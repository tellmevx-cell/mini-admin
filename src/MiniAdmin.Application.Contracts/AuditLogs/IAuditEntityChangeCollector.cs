namespace MiniAdmin.Application.Contracts.AuditLogs;

public interface IAuditEntityChangeCollector
{
    bool IsEnabled { get; }

    void Enable();

    void Disable();

    void AddRange(IReadOnlyList<CapturedAuditEntityChange> changes);

    IReadOnlyList<CapturedAuditEntityChange> GetChanges();
}
