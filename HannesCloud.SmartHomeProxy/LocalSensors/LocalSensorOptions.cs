namespace HannesCloud.SmartHomeProxy.LocalSensors;

public class LocalSensorOptions
{
    /// <summary>
    /// The key the Pi sends in <c>X-Api-Key</c>. Empty switches the endpoint off: anything
    /// on the LAN could otherwise put readings into the cloud.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
