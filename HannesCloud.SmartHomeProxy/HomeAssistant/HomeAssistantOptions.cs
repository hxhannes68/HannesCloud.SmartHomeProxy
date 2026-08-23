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

    /// <summary>
    /// Entity ids with a single "*" standing for the device part. Tasmota puts the device
    /// name in the middle of its energy entities — sensor.steckdose_waschmaschine_energy_power,
    /// sensor.steckdose_trockner_energy_power — so "sensor.steckdose_*_energy_power" covers
    /// every plug regardless of what each one is called, where a plain prefix would need one
    /// entry per device and a plain suffix would let through anything else that happens to
    /// end in _energy_power (a utility_meter helper, a PV inverter) from any domain.
    /// </summary>
    public List<string> AllowedEntityIdPatterns { get; set; } = [];

    public bool Matches(string entityId)
    {
        // Exact match wins immediately
        if (AllowedEntityIds.Contains(entityId))
            return true;

        // Prefix match
        if (AllowedEntityIdPrefixes.Any(prefix =>
                entityId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Wildcard match
        if (AllowedEntityIdPatterns.Any(pattern => MatchesPattern(entityId, pattern)))
            return true;

        // An empty filter forwards everything
        if (AllowedDomains.Count == 0 && AllowedEntityIds.Count == 0
            && AllowedEntityIdPrefixes.Count == 0 && AllowedEntityIdPatterns.Count == 0)
            return true;

        var domain = entityId.Contains('.') ? entityId[..entityId.IndexOf('.')] : entityId;
        return AllowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesPattern(string entityId, string pattern)
    {
        var star = pattern.IndexOf('*');
        if (star < 0)
            return entityId.Equals(pattern, StringComparison.OrdinalIgnoreCase);

        var prefix = pattern[..star];
        var suffix = pattern[(star + 1)..];

        // Both halves have to fit without overlapping, or "a*a" would match "a".
        if (entityId.Length < prefix.Length + suffix.Length)
            return false;

        return entityId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
               && entityId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }
}
