using MiniAdmin.Application.Contracts.AuditLogs;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class AuditEntityChangeCollector : IAuditEntityChangeCollector
{
    private readonly List<CapturedAuditEntityChange> changes = [];

    public bool IsEnabled { get; private set; }

    public void Enable()
    {
        IsEnabled = true;
        changes.Clear();
    }

    public void Disable()
    {
        IsEnabled = false;
    }

    public void AddRange(IReadOnlyList<CapturedAuditEntityChange> entityChanges)
    {
        if (!IsEnabled || entityChanges.Count == 0)
        {
            return;
        }

        changes.AddRange(entityChanges);
    }

    public IReadOnlyList<CapturedAuditEntityChange> GetChanges()
    {
        return changes.ToArray();
    }
}
