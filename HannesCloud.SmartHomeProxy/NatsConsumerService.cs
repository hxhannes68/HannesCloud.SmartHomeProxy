using System.Text.Json;
using HannesCloud.Messages.SmartHome;
using HannesCloud.SmartHomeProxy.Cloud;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.Serializers.Json;

namespace HannesCloud.SmartHomeProxy;

/// <summary>
/// Pulls the 8 light/cover/climate command messages off the household's own NATS
/// stream and calls Home Assistant directly — replaces the 8 MassTransit/SQS
/// consumers that used to do the same, one class each.
///
/// One stream per household rather than one per message type: the backend's NATS
/// broker scopes the "smarthomeproxy" user to exactly this stream (see the backend's
/// nats/nats.conf), so a stream per type would need the config to list each one by
/// name. A single stream with several subject filters needs listing only once, at the
/// cost of dispatching by subject here instead of by NATS subscription.
/// </summary>
public class NatsConsumerService(
    IOptions<NatsOptions> natsOptions,
    IOptions<CloudOptions> cloudOptions,
    HomeAssistantRestClient restClient,
    ILogger<NatsConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var userToken = cloudOptions.Value.OwnerUserId.ToString("N");
        var streamName = $"smarthome_{userToken}";
        var handlers = BuildHandlers(userToken);

        await using var connection = new NatsConnection(new NatsOpts
        {
            Url = natsOptions.Value.Url,
            SerializerRegistry = NatsJsonSerializerRegistry.Default
        });
        var jetStream = new NatsJSContext(connection);

        await jetStream.CreateStreamAsync(new StreamConfig(streamName, handlers.Keys.ToArray())
        {
            Retention = StreamConfigRetention.Workqueue
        }, stoppingToken);

        var consumer = await jetStream.CreateOrUpdateConsumerAsync(streamName,
            new ConsumerConfig("smarthomeproxy")
            {
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                // A message this consumer can never process should not come back forever.
                MaxDeliver = 5,
                Backoff = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
            }, stoppingToken);

        await foreach (var msg in consumer.ConsumeAsync<JsonElement>(cancellationToken: stoppingToken))
        {
            try
            {
                if (handlers.TryGetValue(msg.Subject!, out var handle))
                {
                    await handle(msg.Data.GetRawText(), stoppingToken);
                }
                else
                {
                    logger.LogWarning("Unrecognised subject {Subject}, skipping", msg.Subject);
                }

                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed handling message on {Subject}", msg.Subject);
                await msg.NakAsync(cancellationToken: stoppingToken);
            }
        }
    }

    private Dictionary<string, Func<string, CancellationToken, Task>> BuildHandlers(string userToken) =>
        new()
        {
            [SubjectFor<CloseCoverMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<CloseCoverMessage>(json)!;
                logger.LogInformation("Closing cover {EntityId}", msg.EntityId);
                await restClient.CallServiceAsync("cover", "close_cover", new { entity_id = msg.EntityId }, ct);
            },
            [SubjectFor<OpenCoverMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<OpenCoverMessage>(json)!;
                logger.LogInformation("Opening cover {EntityId}", msg.EntityId);
                await restClient.CallServiceAsync("cover", "open_cover", new { entity_id = msg.EntityId }, ct);
            },
            [SubjectFor<StopCoverMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<StopCoverMessage>(json)!;
                logger.LogInformation("Stopping cover {EntityId}", msg.EntityId);
                await restClient.CallServiceAsync("cover", "stop_cover", new { entity_id = msg.EntityId }, ct);
            },
            [SubjectFor<SetCoverPositionMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<SetCoverPositionMessage>(json)!;
                logger.LogInformation("Setting cover {EntityId} to position {Position}%", msg.EntityId,
                    msg.Position);
                await restClient.CallServiceAsync("cover", "set_cover_position",
                    new { entity_id = msg.EntityId, position = msg.Position }, ct);
            },
            [SubjectFor<TurnOnLightMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<TurnOnLightMessage>(json)!;
                logger.LogInformation("Turning on light {EntityId} (brightness={Brightness})", msg.EntityId,
                    msg.Brightness);
                var data = new Dictionary<string, object> { ["entity_id"] = msg.EntityId };
                if (msg.Brightness is not null) data["brightness"] = msg.Brightness;
                if (msg.RgbColor is not null) data["rgb_color"] = msg.RgbColor;
                if (msg.ColorTemp is not null) data["color_temp"] = msg.ColorTemp;
                await restClient.CallServiceAsync("light", "turn_on", data, ct);
            },
            [SubjectFor<TurnOffLightMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<TurnOffLightMessage>(json)!;
                logger.LogInformation("Turning off light {EntityId}", msg.EntityId);
                await restClient.CallServiceAsync("light", "turn_off", new { entity_id = msg.EntityId }, ct);
            },
            [SubjectFor<SetClimateTemperatureMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<SetClimateTemperatureMessage>(json)!;
                logger.LogInformation("Setting temperature {Temp}° for {EntityId}", msg.Temperature, msg.EntityId);
                await restClient.CallServiceAsync("climate", "set_temperature",
                    new { entity_id = msg.EntityId, temperature = msg.Temperature }, ct);
            },
            [SubjectFor<SetClimateHvacModeMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<SetClimateHvacModeMessage>(json)!;
                logger.LogInformation("Setting hvac_mode {Mode} for {EntityId}", msg.HvacMode, msg.EntityId);
                await restClient.CallServiceAsync("climate", "set_hvac_mode",
                    new { entity_id = msg.EntityId, hvac_mode = msg.HvacMode }, ct);
            }
        };

    private static string SubjectFor<T>(string userToken) =>
        $"{typeof(T).FullName!.ToLowerInvariant()}.{userToken}";
}
