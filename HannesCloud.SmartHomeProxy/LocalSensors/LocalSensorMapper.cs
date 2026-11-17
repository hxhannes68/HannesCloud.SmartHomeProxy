using System.Globalization;
using HannesCloud.SmartHomeProxy.HomeAssistant.Models;

namespace HannesCloud.SmartHomeProxy.LocalSensors;

/// <summary>
/// Turns a reading into the same entity states Home Assistant would send, so the cloud
/// treats the Pi like any other sensor: one entity per measurement, with a device class
/// and unit, named after the device rather than the room so its history stays one series
/// when the Pi is carried to another room.
/// </summary>
public static class LocalSensorMapper
{
    public static IReadOnlyList<EntityState> ToStates(LocalSensorReading reading, DateTimeOffset now)
    {
        var states = new List<EntityState>();
        Add(states, reading, now, reading.Temperature, "temperature", "Temperatur", "°C", 1);
        Add(states, reading, now, reading.Humidity, "humidity", "Luftfeuchtigkeit", "%", 1);
        Add(states, reading, now, reading.Pressure, "atmospheric_pressure", "Luftdruck", "hPa", 1, idSuffix: "pressure");
        return states;
    }

    private static void Add(
        List<EntityState> states, LocalSensorReading reading, DateTimeOffset now,
        double? value, string deviceClass, string label, string unit, int digits, string? idSuffix = null)
    {
        if (value is null)
        {
            return;
        }

        var attributes = new Dictionary<string, object?>
        {
            ["friendly_name"] = $"{reading.Device} {label}",
            ["device_class"] = deviceClass,
            ["unit_of_measurement"] = unit,
            ["state_class"] = "measurement",
            ["source"] = "pi",
        };

        var room = reading.Room?.Trim();
        if (!string.IsNullOrEmpty(room))
        {
            attributes["room"] = room;
        }

        states.Add(new EntityState
        {
            EntityId = $"sensor.{reading.Device}_{idSuffix ?? deviceClass}",
            State = Math.Round(value.Value, digits).ToString(CultureInfo.InvariantCulture),
            Attributes = attributes,
            LastChanged = now,
            LastUpdated = now,
        });
    }
}
