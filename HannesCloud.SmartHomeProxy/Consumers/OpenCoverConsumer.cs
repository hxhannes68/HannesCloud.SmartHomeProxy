using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;

namespace HannesCloud.SmartHomeProxy.Consumers;

public class OpenCoverConsumer(
    HomeAssistantRestClient restClient,
    ILogger<OpenCoverConsumer> logger)
    : IConsumer<OpenCoverMessage>
{
    public async Task Consume(ConsumeContext<OpenCoverMessage> context)
    {
        logger.LogInformation("Opening cover {EntityId}", context.Message.EntityId);

        await restClient.CallServiceAsync("cover", "open_cover", new
        {
            entity_id = context.Message.EntityId
        }, context.CancellationToken);
    }
}
