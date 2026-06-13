namespace HannesCloud.Messages.SmartHome;

public record TurnOnLightMessage(
    string EntityId,
    Guid UserId,
    int? Brightness = null,
    int[]? RgbColor = null,
    int? ColorTemp = null);
