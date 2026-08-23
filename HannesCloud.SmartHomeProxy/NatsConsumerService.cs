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
/// Pulls the 10 light/cover/climate/switch command messages off the household's own NATS
/// stream and calls Home Assistant directly — replaces the MassTransit/SQS
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
    IOptions<HomeAssistantOptions> homeAssistantOptions,
    HomeAssistantRestClient restClient,
    ILogger<NatsConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var userToken = cloudOptions.Value.OwnerUserId.ToString("N");
        var streamName = $"smarthome_{userToken}";
        var handlers = BuildHandlers(userToken);

        // NATS runs on the other side of a Tailscale hop from here. A host crash on a
        // BackgroundService takes the whole process down (Worker, the HA websocket,
        // everything) by default — losing the tailnet route for a minute must not cost
        // us Home Assistant connectivity too, so a broken connection is retried here
        // rather than allowed to propagate.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(streamName, handlers, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NATS connection lost, retrying in 15s");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    private async Task RunAsync(string streamName, Dictionary<string, Func<string, CancellationToken, Task>> handlers,
        CancellationToken stoppingToken)
    {
        await using var connection = new NatsConnection(new NatsOpts
        {
            Url = natsOptions.Value.Url,
            SerializerRegistry = NatsJsonSerializerRegistry.Default
        });
        var jetStream = new NatsJSContext(connection);

        await EnsureStreamAsync(jetStream, streamName, handlers.Keys, stoppingToken);

        var consumer = await jetStream.CreateOrUpdateConsumerAsync(streamName,
            new ConsumerConfig("smarthomeproxy")
            {
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                // A message this consumer can never process should not come back forever.
                MaxDeliver = 5,
                Backoff = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
            }, stoppingToken);

        logger.LogInformation("Connected to NATS, consuming {Stream}", streamName);

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
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Failed handling message on {Subject}", msg.Subject);
                await msg.NakAsync(cancellationToken: stoppingToken);
            }
        }
    }

    /// <summary>
    /// Create *or update*: the subject list is this class's handler table, so adding a
    /// message type changes the config of a stream that already exists. A plain create
    /// would fail with 10058 and take every other command down with it.
    ///
    /// The new subjects are unioned onto whatever the stream already carries rather than
    /// replacing them, because an update is destructive in the other direction too: an
    /// older build of this service — a rollback, or a second container that has not been
    /// updated yet — has a shorter handler table, and a plain overwrite would drop the
    /// subjects it does not know about while the backend is still publishing on them.
    /// </summary>
    private async Task EnsureStreamAsync(NatsJSContext jetStream, string streamName,
        IEnumerable<string> subjects, CancellationToken ct)
    {
        var wanted = new HashSet<string>(subjects, StringComparer.Ordinal);
        var carriedOver = (await ExistingSubjectsAsync(jetStream, streamName, ct))
            .Where(existing => wanted.Add(existing))
            .ToList();

        if (carriedOver.Count > 0)
            logger.LogWarning("Stream {Stream} carries {Count} subject(s) this build does not handle, " +
                              "keeping them: {Subjects}", streamName, carriedOver.Count, carriedOver);

        await jetStream.CreateOrUpdateStreamAsync(new StreamConfig(streamName, wanted.ToArray())
        {
            Retention = StreamConfigRetention.Workqueue
        }, ct);
    }

    /// <summary>
    /// Best effort: reading the config needs $JS.API.STREAM.INFO on the household stream,
    /// which the broker may not grant. Failing to read it is not worth taking the service
    /// down for — fall back to the handler table alone, which is what we would have sent
    /// anyway before this became a union.
    /// </summary>
    private async Task<IReadOnlyList<string>> ExistingSubjectsAsync(NatsJSContext jetStream, string streamName,
        CancellationToken ct)
    {
        try
        {
            var stream = await jetStream.GetStreamAsync(streamName, cancellationToken: ct);
            return stream.Info.Config.Subjects?.ToList() ?? [];
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read the existing config of {Stream}, using the handler table as-is",
                streamName);
            return [];
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
            [SubjectFor<TurnOnSwitchMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<TurnOnSwitchMessage>(json)!;
                if (!IsControllable(msg.EntityId)) return;
                logger.LogInformation("Turning on switch {EntityId}", msg.EntityId);
                await restClient.CallServiceAsync("switch", "turn_on", new { entity_id = msg.EntityId }, ct);
            },
            [SubjectFor<TurnOffSwitchMessage>(userToken)] = async (json, ct) =>
            {
                var msg = JsonSerializer.Deserialize<TurnOffSwitchMessage>(json)!;
                if (!IsControllable(msg.EntityId)) return;
                logger.LogInformation("Turning off switch {EntityId}", msg.EntityId);
                await restClient.CallServiceAsync("switch", "turn_off", new { entity_id = msg.EntityId }, ct);
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

    /// <summary>
    /// The entity filter decides what this household exposes to the cloud, and the switch
    /// domain holds more than the plugs — Home Assistant's own automation toggles live
    /// there too. Listing already skips them; the command path is the side that actually
    /// reaches the house, so it checks the same filter before spending the long-lived HA
    /// token on an entity id that arrived over the wire.
    /// </summary>
    private bool IsControllable(string entityId)
    {
        if (homeAssistantOptions.Value.EntityFilter.Matches(entityId))
            return true;

        logger.LogWarning("Refusing switch command for {EntityId}: outside the entity filter", entityId);
        return false;
    }

    private static string SubjectFor<T>(string userToken) =>
        $"{typeof(T).FullName!.ToLowerInvariant()}.{userToken}";
}
