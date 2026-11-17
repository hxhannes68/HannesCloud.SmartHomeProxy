using System.Globalization;
using HannesCloud.SmartHomeProxy.HomeAssistant.Models;

namespace HannesCloud.SmartHomeProxy.LocalSensors;

/// <summary>
/// Decides which readings are worth sending. The Pi reports every minute; the cloud keeps
/// a history row per state it receives, and a minute-by-minute line of 21.3 °C says nothing
/// the change-based one doesn't. A changed room always goes at once, so a move shows up
/// right away; and every value goes at least every few minutes, so the cloud can tell a
/// quiet sensor from a dead one.
/// </summary>
public class LocalSensorThrottle(TimeProvider time)
{
    public static readonly TimeSpan Heartbeat = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, (double Value, string? Room, DateTimeOffset At)> sent = [];
    private readonly Lock gate = new();

    public IReadOnlyList<EntityState> Filter(IReadOnlyList<EntityState> states) =>
        states.Where(this.ShouldSend).ToList();

    /// <summary>Remembers what went out. Called only once the cloud took it, so a failed send is retried next minute.</summary>
    public void MarkSent(IEnumerable<EntityState> states)
    {
        var now = time.GetUtcNow();
        lock (this.gate)
        {
            foreach (var state in states)
            {
                this.sent[state.EntityId] = (Parse(state.State), RoomOf(state), now);
            }
        }
    }

    private bool ShouldSend(EntityState state)
    {
        lock (this.gate)
        {
            if (!this.sent.TryGetValue(state.EntityId, out var last))
            {
                return true;
            }

            if (!string.Equals(last.Room, RoomOf(state), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (time.GetUtcNow() - last.At >= Heartbeat)
            {
                return true;
            }

            return Math.Abs(Parse(state.State) - last.Value) >= Step(state);
        }
    }

    /// <summary>The smallest change worth sending: below the sensor's own noise, above it.</summary>
    private static double Step(EntityState state) =>
        state.Attributes.GetValueOrDefault("device_class") as string switch
        {
            "temperature" => 0.1,
            "humidity" => 1.0,
            _ => 0.5,
        };

    private static string? RoomOf(EntityState state) => state.Attributes.GetValueOrDefault("room") as string;

    private static double Parse(string value) => double.Parse(value, CultureInfo.InvariantCulture);
}
