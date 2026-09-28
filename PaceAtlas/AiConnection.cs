using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace PaceAtlas;

internal static class AiConnection
{
    private const string CredentialName = "PaceAtlas/OpenAI/API";
    private const int GenericCredential = 1;
    private const int LocalMachinePersistence = 2;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(2) };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags, Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist, AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, int flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);

    public static string? ReadKey()
    {
        if (!CredRead(CredentialName, GenericCredential, 0, out var pointer)) return null;
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            return credential.CredentialBlobSize > 0
                ? Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2) : null;
        }
        finally { CredFree(pointer); }
    }
    public static void SaveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            CredDelete(CredentialName, GenericCredential, 0);
            return;
        }
        var bytes = Encoding.Unicode.GetBytes(key.Trim());
        if (bytes.Length > 2560) throw new ArgumentException("API key is too long.");
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                Type = GenericCredential, TargetName = CredentialName, CredentialBlob = blob,
                CredentialBlobSize = bytes.Length, Persist = LocalMachinePersistence, UserName = "PaceAtlas"
            };
            if (!CredWrite(ref credential, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.FreeCoTaskMem(blob); }
    }
    public static async Task<string> AnalyzeAsync(string key, string model, string input)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model, store = false, max_output_tokens = 2200,
            instructions = "You are analyzing a self-reported ME/CFS pacing log. Respond in the language of the user's request. Separate observations, plausible hypotheses, and unknowns. Put each heading on its own line, followed by a blank line. Start each bullet on its own line. Never assume gaps indicate recovery, absence of symptoms, or that a medication was taken. Symptoms are point-in-time observations; activity and sleep are intervals. An activity with a null End is ongoing and has no known completed duration. Do not claim causal effects from correlations. Do not diagnose or prescribe medication changes. Offer actionable questions for pacing and mention how many records support a pattern. Be concise and specific.",
            input
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode.ToString();
            try
            {
                using var error = JsonDocument.Parse(body);
                message = error.RootElement.GetProperty("error").GetProperty("message").GetString() ?? message;
            }
            catch (JsonException) { }
            throw new InvalidOperationException($"API ({(int)response.StatusCode}): {message}");
        }
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var texts = new List<string>();
        if (root.TryGetProperty("output", out var output))
            foreach (var item in output.EnumerateArray())
                if (item.TryGetProperty("content", out var content))
                    foreach (var part in content.EnumerateArray())
                        if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text"
                            && part.TryGetProperty("text", out var text)) texts.Add(text.GetString() ?? "");
        if (texts.Count > 0) return string.Join("\n", texts);
        throw new InvalidOperationException("The API returned no completed text response.");
    }
}
