using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;

namespace HannesCloud.SmartHomeProxy.Consumers;

public class TurnOffLightConsumer(
    HomeAssistantRestClient restClient,
    ILogger<TurnOffLightConsumer> logger)
    : IConsumer<TurnOffLightMessage>
{
    public async Task Consume(ConsumeContext<TurnOffLightMessage> context)
    {
        logger.LogInformation("Turning off light {EntityId}", context.Message.EntityId);

        await restClient.CallServiceAsync("light", "turn_off", new
        {
            entity_id = context.Message.EntityId
        }, context.CancellationToken);
    }
}
