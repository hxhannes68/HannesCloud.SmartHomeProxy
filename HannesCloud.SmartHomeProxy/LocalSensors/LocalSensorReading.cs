using System.Text.RegularExpressions;

namespace HannesCloud.SmartHomeProxy.LocalSensors;

/// <summary>
/// What a sensor on the LAN (the Raspberry Pi) posts: one device, the room it says it
/// stands in, and whichever values it measures.
/// </summary>
public partial class LocalSensorReading
{
    public string Device { get; set; } = string.Empty;
    public string? Room { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public double? Pressure { get; set; }

    /// <summary>The problem with this reading in words for the sender, or null when it is fine.</summary>
    public string? Validate()
    {
        if (!DeviceName().IsMatch(this.Device))
        {
            return "device must be 1–40 lowercase letters, digits or underscores, e.g. \"pi1\".";
        }

        if (this.Temperature is null && this.Humidity is null && this.Pressure is null)
        {
            return "send at least one of temperature, humidity or pressure.";
        }

        if (this.Room is { Length: > 100 })
        {
            return "room must be at most 100 characters.";
        }

        return OutOfRange(this.Temperature, -40, 85, "temperature")
               ?? OutOfRange(this.Humidity, 0, 100, "humidity")
               ?? OutOfRange(this.Pressure, 300, 1100, "pressure");
    }

    // A sensor that misreads (a loose wire on the I2C bus gives -142 °C) must not reach the
    // cloud, where it would raise an alert or ruin a chart.
    private static string? OutOfRange(double? value, double min, double max, string name)
    {
        if (value is null || (value >= min && value <= max && double.IsFinite(value.Value)))
        {
            return null;
        }

        return $"{name} {value} is outside {min}…{max}.";
    }

    [GeneratedRegex("^[a-z0-9_]{1,40}$")]
    private static partial Regex DeviceName();
}
