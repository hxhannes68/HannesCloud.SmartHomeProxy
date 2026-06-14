namespace HannesCloud.SmartHomeProxy;

public class ServiceBusOptions
{
    public string AccessKey { get; set; } = string.Empty;
    public string AccessSecret { get; set; } = string.Empty;
    public string Queue { get; set; } = string.Empty;

    /// <summary>
    /// SQS long polling wait time in seconds (0 = short polling, 20 = max / recommended).
    /// Long polling drastically reduces the number of empty ReceiveMessage API calls.
    /// </summary>
    public ushort WaitTimeSeconds { get; set; } = 20;
}
