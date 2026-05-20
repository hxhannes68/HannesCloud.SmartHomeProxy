namespace HannesCloud.SmartHomeProxy;

public class ServiceBusOptions
{
    public string AccessKey { get; set; } = string.Empty;
    public string AccessSecret { get; set; } = string.Empty;
    public string Queue { get; set; } = string.Empty;
}
