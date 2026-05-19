using System.Net.Http.Headers;
using System.Text.Json;
using HannesCloud.SmartHomeProxy.HomeAssistant.Models;
using Microsoft.Extensions.Options;

namespace HannesCloud.SmartHomeProxy.HomeAssistant;

public class HomeAssistantRestClient(HttpClient httpClient, IOptions<HomeAssistantOptions> options, ILogger<HomeAssistantRestClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<EntityState>> GetAllStatesAsync(CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/states");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);

        logger.LogDebug("Fetching all entity states from HomeAssistant REST API");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var states = JsonSerializer.Deserialize<List<EntityState>>(json, JsonOptions) ?? [];

        logger.LogInformation("Fetched {Count} entity states from HomeAssistant", states.Count);
        return states;
    }
}
