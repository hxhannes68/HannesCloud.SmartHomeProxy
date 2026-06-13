using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;

namespace HannesCloud.SmartHomeProxy.Consumers;

public class TurnOnLightConsumer(
    HomeAssistantRestClient restClient,
    ILogger<TurnOnLightConsumer> logger)
    : IConsumer<TurnOnLightMessage>
{
    public async Task Consume(ConsumeContext<TurnOnLightMessage> context)
    {
        var msg = context.Message;
        logger.LogInformation("Turning on light {EntityId} (brightness={Brightness})", msg.EntityId, msg.Brightness);

        await restClient.CallServiceAsync("light", "turn_on", new
        {
            entity_id = msg.EntityId,
            brightness = msg.Brightness,
            rgb_color = msg.RgbColor,
            color_temp = msg.ColorTemp,
        }, context.CancellationToken);
    }
}
