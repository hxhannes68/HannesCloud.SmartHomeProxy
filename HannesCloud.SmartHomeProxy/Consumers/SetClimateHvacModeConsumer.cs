using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;

namespace HannesCloud.SmartHomeProxy.Consumers;

public class SetClimateHvacModeConsumer(
    HomeAssistantRestClient restClient,
    ILogger<SetClimateHvacModeConsumer> logger)
    : IConsumer<SetClimateHvacModeMessage>
{
    public async Task Consume(ConsumeContext<SetClimateHvacModeMessage> context)
    {
        logger.LogInformation("Setting hvac_mode {Mode} for {EntityId}",
            context.Message.HvacMode, context.Message.EntityId);

        await restClient.CallServiceAsync("climate", "set_hvac_mode", new
        {
            entity_id = context.Message.EntityId,
            hvac_mode = context.Message.HvacMode
        }, context.CancellationToken);
    }
}
