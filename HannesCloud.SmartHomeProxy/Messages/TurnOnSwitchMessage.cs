namespace HannesCloud.Messages.SmartHome;

public record TurnOnSwitchMessage(string EntityId, Guid UserId);
