namespace PaceAtlas.IPC;

/// <summary>In-process notification emitted only after a successful API write.</summary>
public sealed class DataChangeNotifier
{
    public event Action<DataChangeKind>? Changed;

    public void Publish(DataChangeKind kind) => Changed?.Invoke(kind);
}

[Flags]
public enum DataChangeKind
{
    None = 0,
    Status = 1,
    Activities = 2,
    Medications = 4,
    Measures = 8
}
