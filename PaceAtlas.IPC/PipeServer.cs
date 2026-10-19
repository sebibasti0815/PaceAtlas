using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace PaceAtlas.IPC;

public sealed class PipeServer : IDisposable
{
    public const string PipeName = "PaceAtlas.Api.v1";
    private readonly PaceAtlasApi api;
    private readonly CancellationTokenSource shutdown = new();
    // Keep several independent pipe instances waiting at all times. A single
    // accept loop leaves a short interval with no listener between connections.
    private const int ListenerCount = 4;
    private const int MaxClients = 32;
    private readonly SemaphoreSlim clientSlots = new(MaxClients, MaxClients);
    private readonly object lifecycle = new();
    private static readonly object logSync = new();
    // Opt-in diagnostics. Never log JSON-RPC parameters, responses or health records.
    private static long verboseUntilUtcTicks;
    public static bool IsVerboseDiagnosticsActive =>
        string.Equals(Environment.GetEnvironmentVariable("PACEATLAS_IPC_DIAGNOSTICS"), "1", StringComparison.Ordinal) ||
        DateTime.UtcNow.Ticks < Interlocked.Read(ref verboseUntilUtcTicks);

    public static void EnableVerboseDiagnostics(TimeSpan duration) =>
        Interlocked.Exchange(ref verboseUntilUtcTicks, DateTime.UtcNow.Add(duration).Ticks);

    public static void DisableVerboseDiagnostics() =>
        Interlocked.Exchange(ref verboseUntilUtcTicks, 0);
    private readonly HashSet<Task> handlers = new();
    private Task[]? workers;
    private bool disposed;

    public PipeServer(PaceAtlasApi api) => this.api = api;

    public void Start()
    {
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            workers ??= Enumerable.Range(0, ListenerCount)
                .Select(_ => Task.Run(() => AcceptAsync(shutdown.Token)))
                .ToArray();
        }
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        var sid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("No Windows SID");
        // Windows checks FILE_CREATE_PIPE_INSTANCE when additional server instances
        // are created under the same name. ReadWrite alone does NOT grant it.
        // Without this permission the first listener succeeds and the others
        // repeatedly fail with UnauthorizedAccessException.
        security.AddAccessRule(new PipeAccessRule(
            sid,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
    }
    private async Task AcceptAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            bool slotAcquired = false;
            try
            {
                // Limit accepted clients without blocking the listener thread.
                await clientSlots.WaitAsync(token);
                slotAcquired = true;
                pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(token);
                var connected = pipe;
                pipe = null;
                slotAcquired = false; // The handler now owns this slot.
                var task = ServeClientAndReleaseAsync(connected, token);
                lock (lifecycle) handlers.Add(task);
                _ = task.ContinueWith(completed =>
                {
                    lock (lifecycle) handlers.Remove(completed);
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (UnauthorizedAccessException ex)
            {
                // A permanent ACL/configuration failure must not create a
                // tight exception loop (or continuously hammer the log file).
                Log(ex);
                break;
            }
            catch (Exception ex)
            {
                Log(ex);
                try { await Task.Delay(250, token); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                pipe?.Dispose();
                if (slotAcquired) clientSlots.Release();
            }
        }
    }

    private async Task ServeClientAndReleaseAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        try { await ServeClientAsync(pipe, token); }
        finally { clientSlots.Release(); }
    }

    private async Task ServeClientAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        using (pipe)
        {
            try { await HandleAsync(pipe, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (IOException) { } // Client disconnected.
            catch (Exception ex) { Log(ex); }
        }
    }
    private static void Log(Exception ex)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaceAtlas", "ipc-error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Multiple accept loops can fail simultaneously. Serialize writes
            // so a logging failure does not trigger another exception storm.
            lock (logSync)
                File.AppendAllText(path, $"{DateTime.Now:O} {ex}\n");
        }
        catch { }
    }
    private static void LogRequest(string method, long elapsedMs, bool success, string correlationId)
    {
        if (success && !IsVerboseDiagnosticsActive && elapsedMs < 500) return;
        try
        {
            // Method identifiers only: no arguments, notes, health data or identifiers.
            var safeMethod = new string(method.Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-').Take(80).ToArray());
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaceAtlas", "Logs", "ipc-diagnostics.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            lock (logSync)
            {
                // Keep up to three bounded generations. Rotation is serialized with writes.
                const long maxLogBytes = 2L * 1024 * 1024;
                if (File.Exists(path) && new FileInfo(path).Length >= maxLogBytes)
                {
                    for (var i = 3; i >= 1; i--)
                    {
                        var previous = i == 1 ? path : path + "." + (i - 1);
                        var next = path + "." + i;
                        if (File.Exists(previous)) File.Move(previous, next, true);
                    }
                }
                File.AppendAllText(path, $"{DateTime.UtcNow:O} {(success ? "OK" : "ERROR")} method={safeMethod} durationMs={elapsedMs} correlationId={correlationId}\n");
            }
        }
        catch { } // Logging must never interrupt an IPC request.
    }

    private async Task HandleAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[4];
        while (!token.IsCancellationRequested)
        {
            try { await stream.ReadExactlyAsync(header, token); }
            catch (EndOfStreamException) { return; }
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length <= 0 || length > 1024 * 1024) return;
            var buffer = new byte[length];
            await stream.ReadExactlyAsync(buffer, token);
            object response;
            object? id = null;
            var stopwatch = Stopwatch.StartNew();
            var requestMethod = "unknown";
            // Random per-request correlation ID: never derived from user data or JSON-RPC parameters.
            var correlationId = Guid.NewGuid().ToString("N");
            var requestSucceeded = false;
            try
            {
                using var doc = JsonDocument.Parse(buffer);
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var identifier)) id = identifier.Clone();
                if (root.GetProperty("jsonrpc").GetString() != "2.0") throw new ArgumentException("Expected JSON-RPC 2.0");
                var method = root.GetProperty("method").GetString() ?? throw new ArgumentException("Missing method");
                requestMethod = method;
                var parameters = root.TryGetProperty("params", out var p) ? p : default;
                response = new { jsonrpc = "2.0", id, result = api.Execute(method, parameters) };
                requestSucceeded = true;
            }
            catch (Exception ex)
            {
                var code = ex is MissingMethodException ? -32601 : ex is JsonException or KeyNotFoundException ? -32700 : ex is ArgumentException ? -32602 : -32000;
                response = new { jsonrpc = "2.0", id, error = new { code, message = ex.Message } };
            }
            stopwatch.Stop();
            LogRequest(requestMethod, stopwatch.ElapsedMilliseconds, requestSucceeded, correlationId);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
            await stream.WriteAsync(header, token);
            await stream.WriteAsync(bytes, token);
            await stream.FlushAsync(token);
        }
    }
    public void Dispose()
    {
        lock (lifecycle)
        {
            if (disposed) return;
            disposed = true;
            shutdown.Cancel();
        }
        // Cancellation interrupts WaitForConnectionAsync and in-flight I/O.
        // Listener/handler tasks dispose their own pipe instances in finally.
        // Do not synchronously wait here: WinUI may call Dispose on its UI thread.
    }
}
