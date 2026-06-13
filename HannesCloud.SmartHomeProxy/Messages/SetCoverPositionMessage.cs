namespace HannesCloud.Messages.SmartHome;

public record SetCoverPositionMessage(string EntityId, int Position, Guid UserId);
