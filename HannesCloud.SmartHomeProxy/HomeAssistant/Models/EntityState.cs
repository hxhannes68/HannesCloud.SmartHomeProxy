using System.Text.Json.Serialization;

namespace HannesCloud.SmartHomeProxy.HomeAssistant.Models;

public class EntityState
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonPropertyName("last_changed")]
    public DateTimeOffset LastChanged { get; set; }

    [JsonPropertyName("last_updated")]
    public DateTimeOffset LastUpdated { get; set; }

    public string Domain => EntityId.Contains('.') ? EntityId[..EntityId.IndexOf('.')] : EntityId;

    public string FriendlyName => Attributes.TryGetValue("friendly_name", out var name)
        ? name?.ToString() ?? EntityId
        : EntityId;
}
