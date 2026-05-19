using System.Text.Json.Serialization;

namespace HannesCloud.SmartHomeProxy.HomeAssistant.Models;

public class WsAuthRequired
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("ha_version")]
    public string HaVersion { get; set; } = string.Empty;
}

public class WsAuthMessage
{
    [JsonPropertyName("type")]
    public string Type => "auth";

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
}

public class WsSubscribeEvents
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type => "subscribe_events";

    [JsonPropertyName("event_type")]
    public string EventType => "state_changed";
}

public class WsResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class WsEvent
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("event")]
    public WsEventData? Event { get; set; }
}

public class WsEventData
{
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public WsStateChangedData? Data { get; set; }
}

public class WsStateChangedData
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("old_state")]
    public EntityState? OldState { get; set; }

    [JsonPropertyName("new_state")]
    public EntityState? NewState { get; set; }
}
