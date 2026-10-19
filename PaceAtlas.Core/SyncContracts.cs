namespace PaceAtlas.Sync;

/// <summary>
/// Transport-neutral synchronization contracts. These types do not activate
/// synchronization or modify the existing SQLite schema.
/// </summary>
public enum SyncMutationKind { Create, Update, Delete }
public enum SyncDecision { Accept, AlreadyApplied, Conflict, Invalid }

public sealed record SyncMutation(
    Guid OperationId,
    Guid RecordId,
    SyncMutationKind Kind,
    long? ExpectedRevision,
    string? Payload);

public sealed record SyncRecordState(Guid RecordId, long Revision, bool IsDeleted);

public sealed record SyncEvaluation(SyncDecision Decision, string Reason);

/// <summary>
/// Conservative, server-side precondition checks. The authoritative server
/// must check OperationId deduplication and commit revisions atomically.
/// </summary>
public static class SyncPreconditions
{
    public static SyncEvaluation Evaluate(SyncMutation mutation, SyncRecordState? current, bool operationAlreadyApplied)
    {
        if (mutation.OperationId == Guid.Empty || mutation.RecordId == Guid.Empty)
            return new(SyncDecision.Invalid, "Missing stable identifier");
        if (operationAlreadyApplied)
            return new(SyncDecision.AlreadyApplied, "Operation was already committed");
        if (mutation.Kind == SyncMutationKind.Create)
        {
            if (mutation.ExpectedRevision is not null || string.IsNullOrWhiteSpace(mutation.Payload))
                return new(SyncDecision.Invalid, "Create requires a payload and no base revision");
            return current is null
                ? new(SyncDecision.Accept, "New record")
                : new(SyncDecision.Conflict, "Record identity already exists, including tombstones");
        }
        if (mutation.ExpectedRevision is null || mutation.ExpectedRevision < 1)
            return new(SyncDecision.Invalid, "Existing records require a positive base revision");
        if (mutation.Kind == SyncMutationKind.Update && string.IsNullOrWhiteSpace(mutation.Payload))
            return new(SyncDecision.Invalid, "Update requires a payload");
        if (current is null)
            return new(SyncDecision.Conflict, "Record not found; it must not be recreated");
        if (current.IsDeleted)
            return new(SyncDecision.Conflict, "Record is deleted; tombstone takes precedence");
        if (current.Revision != mutation.ExpectedRevision.Value)
            return new(SyncDecision.Conflict, "Base revision is stale");
        return new(SyncDecision.Accept, "Revision matches");
    }
}
