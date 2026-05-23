namespace HannesCloud.Messages.SmartHome;

public record SetClimateHvacModeMessage(string EntityId, string HvacMode, Guid UserId);
