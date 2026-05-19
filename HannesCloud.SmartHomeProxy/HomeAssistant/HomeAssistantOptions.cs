namespace HannesCloud.SmartHomeProxy.HomeAssistant;

public class HomeAssistantOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public EntityFilterOptions EntityFilter { get; set; } = new();
}

public class EntityFilterOptions
{
    public List<string> AllowedDomains { get; set; } = [];
    public List<string> AllowedEntityIds { get; set; } = [];
    public List<string> AllowedEntityIdPrefixes { get; set; } = [];

    public bool Matches(string entityId)
    {
        // Exact match wins immediately
        if (AllowedEntityIds.Contains(entityId))
            return true;

        // Prefix match
        if (AllowedEntityIdPrefixes.Any(prefix =>
                entityId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Domain match (only when no prefix/exact filter is set for that domain)
        if (AllowedDomains.Count == 0 && AllowedEntityIds.Count == 0 && AllowedEntityIdPrefixes.Count == 0)
            return true;

        var domain = entityId.Contains('.') ? entityId[..entityId.IndexOf('.')] : entityId;
        return AllowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
    }
}
