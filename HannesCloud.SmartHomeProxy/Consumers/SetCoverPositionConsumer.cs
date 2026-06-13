using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;

namespace HannesCloud.SmartHomeProxy.Consumers;

public class SetCoverPositionConsumer(
    HomeAssistantRestClient restClient,
    ILogger<SetCoverPositionConsumer> logger)
    : IConsumer<SetCoverPositionMessage>
{
    public async Task Consume(ConsumeContext<SetCoverPositionMessage> context)
    {
        logger.LogInformation("Setting cover {EntityId} to position {Position}%",
            context.Message.EntityId, context.Message.Position);

        await restClient.CallServiceAsync("cover", "set_cover_position", new
        {
            entity_id = context.Message.EntityId,
            position = context.Message.Position
        }, context.CancellationToken);
    }
}
