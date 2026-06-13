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

        var data = new Dictionary<string, object> { ["entity_id"] = msg.EntityId };
        if (msg.Brightness is not null) data["brightness"]  = msg.Brightness;
        if (msg.RgbColor  is not null) data["rgb_color"]   = msg.RgbColor;
        if (msg.ColorTemp is not null) data["color_temp"]  = msg.ColorTemp;

        await restClient.CallServiceAsync("light", "turn_on", data, context.CancellationToken);
    }
}
