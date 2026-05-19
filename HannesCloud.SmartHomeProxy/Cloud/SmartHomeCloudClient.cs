using System.Net.Http.Headers;
using System.Net.Http.Json;
using HannesCloud.SmartHomeProxy.HomeAssistant.Models;
using Microsoft.Extensions.Options;

namespace HannesCloud.SmartHomeProxy.Cloud;

public class SmartHomeCloudClient(HttpClient httpClient, Auth0TokenProvider tokenProvider, IOptions<CloudOptions> options, ILogger<SmartHomeCloudClient> logger)
{
    public async Task SendStatesAsync(IReadOnlyList<EntityState> states, CancellationToken ct = default)
    {
        var token = await tokenProvider.GetTokenAsync(ct);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            UserId = options.Value.OwnerUserId,
            States = states.Select(s => new
            {
                s.EntityId,
                s.Domain,
                s.FriendlyName,
                s.State,
                AttributesJson = System.Text.Json.JsonSerializer.Serialize(s.Attributes),
                s.LastChanged,
                s.LastUpdated
            }).ToList()
        };

        var response = await httpClient.PostAsJsonAsync("smarthome/ingest", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Cloud ingest returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            return;
        }

        logger.LogDebug("Sent {Count} states to cloud", states.Count);
    }
}
