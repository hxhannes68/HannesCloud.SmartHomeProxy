namespace HannesCloud.Messages.SmartHome;

public record SetClimateTemperatureMessage(string EntityId, double Temperature, Guid UserId);
