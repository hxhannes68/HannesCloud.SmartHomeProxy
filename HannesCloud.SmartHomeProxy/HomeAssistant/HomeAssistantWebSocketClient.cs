using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HannesCloud.SmartHomeProxy.HomeAssistant.Models;
using Microsoft.Extensions.Options;

namespace HannesCloud.SmartHomeProxy.HomeAssistant;

public class HomeAssistantWebSocketClient(IOptions<HomeAssistantOptions> options, ILogger<HomeAssistantWebSocketClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const int SubscribeId = 1;
    private const int BufferSize = 8192;

    public async Task RunAsync(Func<EntityState, CancellationToken, Task> onStateChanged, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(onStateChanged, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "WebSocket connection to HomeAssistant lost. Reconnecting in 10 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
    }

    private async Task ConnectAndListenAsync(Func<EntityState, CancellationToken, Task> onStateChanged, CancellationToken ct)
    {
        var wsUri = BuildWebSocketUri();
        using var ws = new ClientWebSocket();

        logger.LogInformation("Connecting to HomeAssistant WebSocket at {Uri}", wsUri);
        await ws.ConnectAsync(wsUri, ct);
        logger.LogInformation("WebSocket connected");

        await AuthenticateAsync(ws, ct);
        await SubscribeToStateChangesAsync(ws, ct);

        logger.LogInformation("Subscribed to state_changed events");

        await ReceiveLoopAsync(ws, onStateChanged, ct);
    }

    private async Task AuthenticateAsync(ClientWebSocket ws, CancellationToken ct)
    {
        // Receive auth_required
        var authRequired = await ReceiveMessageAsync<WsAuthRequired>(ws, ct);
        if (authRequired?.Type != "auth_required")
            throw new InvalidOperationException($"Expected auth_required, got: {authRequired?.Type}");

        logger.LogDebug("HomeAssistant version: {Version}", authRequired.HaVersion);

        // Send auth
        var authMsg = new WsAuthMessage { AccessToken = options.Value.AccessToken };
        await SendMessageAsync(ws, authMsg, ct);

        // Receive auth_ok or auth_invalid
        var authResult = await ReceiveMessageAsync<WsResult>(ws, ct);
        if (authResult?.Type == "auth_invalid")
            throw new InvalidOperationException("HomeAssistant authentication failed. Check your access token.");

        if (authResult?.Type != "auth_ok")
            throw new InvalidOperationException($"Unexpected auth response: {authResult?.Type}");

        logger.LogInformation("Authenticated with HomeAssistant");
    }

    private static async Task SubscribeToStateChangesAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var subscribe = new WsSubscribeEvents { Id = SubscribeId };
        await SendMessageAsync(ws, subscribe, ct);

        // Receive subscription confirmation
        var result = await ReceiveMessageAsync<WsResult>(ws, ct);
        if (result is not { Success: true })
            throw new InvalidOperationException("Failed to subscribe to state_changed events");
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, Func<EntityState, CancellationToken, Task> onStateChanged, CancellationToken ct)
    {
        var filter = options.Value.EntityFilter;

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var message = await ReceiveRawAsync(ws, ct);
            if (message is null)
                continue;

            using var doc = JsonDocument.Parse(message);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "event")
                continue;

            var wsEvent = JsonSerializer.Deserialize<WsEvent>(message, JsonOptions);
            var newState = wsEvent?.Event?.Data?.NewState;

            if (newState is null)
                continue;

            if (!filter.Matches(newState.EntityId))
                continue;

            await onStateChanged(newState, ct);
        }
    }

    private static async Task SendMessageAsync<T>(ClientWebSocket ws, T message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static async Task<T?> ReceiveMessageAsync<T>(ClientWebSocket ws, CancellationToken ct)
    {
        var raw = await ReceiveRawAsync(ws, ct);
        return raw is null ? default : JsonSerializer.Deserialize<T>(raw, JsonOptions);
    }

    private static async Task<string?> ReceiveRawAsync(ClientWebSocket ws, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[BufferSize];
        WebSocketReceiveResult result;

        do
        {
            result = await ws.ReceiveAsync(buffer, ct);

            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private Uri BuildWebSocketUri()
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        var wsUrl = baseUrl
            .Replace("https://", "wss://")
            .Replace("http://", "ws://");
        return new Uri($"{wsUrl}/api/websocket");
    }
}
