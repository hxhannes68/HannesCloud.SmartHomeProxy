using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;

namespace HannesCloud.SmartHomeProxy.Consumers;

public class SetClimateTemperatureConsumer(
    HomeAssistantRestClient restClient,
    ILogger<SetClimateTemperatureConsumer> logger)
    : IConsumer<SetClimateTemperatureMessage>
{
    public async Task Consume(ConsumeContext<SetClimateTemperatureMessage> context)
    {
        logger.LogInformation("Setting temperature {Temp}° for {EntityId}",
            context.Message.Temperature, context.Message.EntityId);

        await restClient.CallServiceAsync("climate", "set_temperature", new
        {
            entity_id = context.Message.EntityId,
            temperature = context.Message.Temperature
        }, context.CancellationToken);
    }
}
