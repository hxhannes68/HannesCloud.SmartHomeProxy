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
    /// Matches on the tail of the entity id. Tasmota's energy entities are named after
    /// the device and then suffixed — sensor.waschmaschine_energy_power,
    /// sensor.trockner_energy_power — so a single "_energy_power" entry covers every
    /// plug regardless of what each one is called, where a prefix would need one entry
    /// per device.
    /// </summary>
    public List<string> AllowedEntityIdSuffixes { get; set; } = [];

    public bool Matches(string entityId)
    {
        // Exact match wins immediately
        if (AllowedEntityIds.Contains(entityId))
            return true;

        // Prefix match
        if (AllowedEntityIdPrefixes.Any(prefix =>
                entityId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Suffix match
        if (AllowedEntityIdSuffixes.Any(suffix =>
                entityId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            return true;

        // An empty filter forwards everything
        if (AllowedDomains.Count == 0 && AllowedEntityIds.Count == 0
            && AllowedEntityIdPrefixes.Count == 0 && AllowedEntityIdSuffixes.Count == 0)
            return true;

        var domain = entityId.Contains('.') ? entityId[..entityId.IndexOf('.')] : entityId;
        return AllowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
    }
}
