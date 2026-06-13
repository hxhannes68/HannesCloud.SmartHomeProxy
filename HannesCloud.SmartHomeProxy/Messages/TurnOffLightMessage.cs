namespace HannesCloud.Messages.SmartHome;

public record TurnOffLightMessage(string EntityId, Guid UserId);
