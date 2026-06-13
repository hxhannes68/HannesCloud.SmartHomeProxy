using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;

namespace HannesCloud.SmartHomeProxy.Consumers;

public class StopCoverConsumer(
    HomeAssistantRestClient restClient,
    ILogger<StopCoverConsumer> logger)
    : IConsumer<StopCoverMessage>
{
    public async Task Consume(ConsumeContext<StopCoverMessage> context)
    {
        logger.LogInformation("Stopping cover {EntityId}", context.Message.EntityId);

        await restClient.CallServiceAsync("cover", "stop_cover", new
        {
            entity_id = context.Message.EntityId
        }, context.CancellationToken);
    }
}
