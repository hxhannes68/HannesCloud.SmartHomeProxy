namespace HannesCloud.Messages.SmartHome;

public record TurnOffSwitchMessage(string EntityId, Guid UserId);
