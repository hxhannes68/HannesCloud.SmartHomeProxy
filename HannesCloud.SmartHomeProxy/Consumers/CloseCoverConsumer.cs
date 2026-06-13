using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;

namespace HannesCloud.SmartHomeProxy.Consumers;

public class CloseCoverConsumer(
    HomeAssistantRestClient restClient,
    ILogger<CloseCoverConsumer> logger)
    : IConsumer<CloseCoverMessage>
{
    public async Task Consume(ConsumeContext<CloseCoverMessage> context)
    {
        logger.LogInformation("Closing cover {EntityId}", context.Message.EntityId);

        await restClient.CallServiceAsync("cover", "close_cover", new
        {
            entity_id = context.Message.EntityId
        }, context.CancellationToken);
    }
}
