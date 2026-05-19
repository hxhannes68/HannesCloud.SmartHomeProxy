namespace HannesCloud.SmartHomeProxy.Cloud;

public class CloudOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public Auth0Options Auth0 { get; set; } = new();
}

public class Auth0Options
{
    public string Domain { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
