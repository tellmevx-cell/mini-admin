using System.Text.Json;
using MiniAdmin.Application.Contracts.Events;

namespace MiniAdmin.Infrastructure.Events;

public interface IOutboxEventSerializer
{
    (string EventType, string Payload) Serialize(IOutboxEvent @event);

    IOutboxEvent Deserialize(string eventType, string payload);
}

public sealed class OutboxEventSerializer : IOutboxEventSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public (string EventType, string Payload) Serialize(IOutboxEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var type = @event.GetType();
        var assemblyName = type.Assembly.GetName().Name
            ?? throw new InvalidOperationException($"Event assembly name is missing for {type.FullName}.");
        var stableTypeName = $"{type.FullName}, {assemblyName}";
        return (stableTypeName, JsonSerializer.Serialize(@event, type, JsonOptions));
    }

    public IOutboxEvent Deserialize(string eventType, string payload)
    {
        var type = Type.GetType(eventType, throwOnError: false)
            ?? throw new InvalidOperationException($"Outbox event type cannot be resolved: {eventType}.");
        if (!typeof(IOutboxEvent).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Outbox event type does not implement IOutboxEvent: {eventType}.");
        }

        return JsonSerializer.Deserialize(payload, type, JsonOptions) as IOutboxEvent
            ?? throw new InvalidOperationException($"Outbox event payload cannot be deserialized: {eventType}.");
    }
}
