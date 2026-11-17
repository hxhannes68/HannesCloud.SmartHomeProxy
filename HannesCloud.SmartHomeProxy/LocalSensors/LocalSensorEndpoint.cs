using System.Security.Cryptography;
using System.Text;
using HannesCloud.SmartHomeProxy.Cloud;
using Microsoft.Extensions.Options;

namespace HannesCloud.SmartHomeProxy.LocalSensors;

/// <summary>
/// <c>POST /local/states</c>: sensors on the LAN that aren't in Home Assistant hand their
/// readings to the proxy, which forwards them to the cloud like any HA entity.
/// </summary>
public static class LocalSensorEndpoint
{
    public const string KeyHeader = "X-Api-Key";

    public static void MapLocalSensors(this IEndpointRouteBuilder app) =>
        app.MapPost("/local/states", HandleAsync);

    private static async Task<IResult> HandleAsync(
        LocalSensorReading reading,
        HttpRequest request,
        IOptions<LocalSensorOptions> options,
        LocalSensorThrottle throttle,
        TimeProvider time,
        IServiceProvider services,
        ILogger<LocalSensorReading> logger,
        CancellationToken ct)
    {
        if (!IsAuthorized(request, options.Value.ApiKey))
        {
            return Results.Unauthorized();
        }

        var problem = reading.Validate();
        if (problem is not null)
        {
            return Results.BadRequest(new { error = problem });
        }

        var due = throttle.Filter(LocalSensorMapper.ToStates(reading, time.GetUtcNow()));
        if (due.Count == 0)
        {
            return Results.Accepted(value: new { forwarded = 0 });
        }

        // Absent when the proxy runs without a cloud (log-only mode).
        var cloud = services.GetService<SmartHomeCloudClient>();
        if (cloud is null)
        {
            logger.LogInformation("[LOCAL] {Device} in {Room}: {Count} states (cloud disabled)", reading.Device, reading.Room, due.Count);
            return Results.Accepted(value: new { forwarded = 0 });
        }

        if (!await cloud.SendStatesAsync(due, ct))
        {
            // Not marked as sent, so the next reading tries again.
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        throttle.MarkSent(due);
        logger.LogInformation("[LOCAL] {Device} in {Room}: forwarded {Count} states", reading.Device, reading.Room, due.Count);
        return Results.Accepted(value: new { forwarded = due.Count });
    }

    private static bool IsAuthorized(HttpRequest request, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return false;
        }

        var sent = request.Headers[KeyHeader].ToString();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(apiKey));
    }
}
