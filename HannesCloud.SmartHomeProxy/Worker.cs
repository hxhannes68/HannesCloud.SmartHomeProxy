using HannesCloud.SmartHomeProxy.Cloud;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using HannesCloud.SmartHomeProxy.HomeAssistant.Models;
using Microsoft.Extensions.Options;

namespace HannesCloud.SmartHomeProxy;

public class Worker(
    HomeAssistantRestClient restClient,
    HomeAssistantWebSocketClient wsClient,
    SmartHomeCloudClient? cloudClient,
    IOptions<HomeAssistantOptions> options,
    IOptions<CloudOptions> cloudOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly bool _cloudEnabled = !string.IsNullOrEmpty(cloudOptions.Value.BaseUrl);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SmartHome Proxy starting. HA={BaseUrl} Cloud={CloudEnabled}",
            options.Value.BaseUrl, _cloudEnabled ? cloudOptions.Value.BaseUrl : "disabled (logging only)");

        await SyncInitialStatesAsync(stoppingToken);
        await wsClient.RunAsync(OnStateChangedAsync, stoppingToken);

        logger.LogInformation("SmartHome Proxy stopped");
    }

    private async Task SyncInitialStatesAsync(CancellationToken ct)
    {
        try
        {
            var states = await restClient.GetAllStatesAsync(ct);
            var filter = options.Value.EntityFilter;
            var filtered = states.Where(s => filter.Matches(s.EntityId)).ToList();

            logger.LogInformation("Initial sync: {Total} total entities, {Filtered} after filter", states.Count, filtered.Count);

            foreach (var state in filtered)
                LogEntityState("INITIAL", state);

            await ForwardToCloudAsync(filtered, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync initial states from HomeAssistant");
        }
    }

    private async Task OnStateChangedAsync(EntityState newState, CancellationToken ct)
    {
        LogEntityState("CHANGED", newState);
        await ForwardToCloudAsync([newState], ct);
    }

    private async Task ForwardToCloudAsync(IReadOnlyList<EntityState> states, CancellationToken ct)
    {
        if (!_cloudEnabled || cloudClient is null || states.Count == 0)
            return;

        try
        {
            await cloudClient.SendStatesAsync(states, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward {Count} states to cloud", states.Count);
        }
    }

    private void LogEntityState(string trigger, EntityState state)
    {
        logger.LogInformation(
            "[{Trigger}] {EntityId} | {FriendlyName} | state={State} | lastChanged={LastChanged:O}",
            trigger,
            state.EntityId,
            state.FriendlyName,
            state.State,
            state.LastChanged);
    }
}
