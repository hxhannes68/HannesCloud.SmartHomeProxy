namespace HannesCloud.SmartHomeProxy;

/// <summary>
/// How long a queued command stays worth executing. The stream is durable, so after the
/// tailnet route has been down the whole backlog arrives at once — a "light off" from
/// two hours ago would switch the light off now, in a room somebody is sitting in.
///
/// Commands for "now" (lights, switches, moving a cover) go stale quickly. Those that set
/// a target state (cover position, climate) still describe what is wanted after a while.
/// </summary>
public static class CommandFreshness
{
    public static readonly TimeSpan ForImmediateCommands = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ForTargetStates = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The stream drops what is older than the longest limit above, so an outage does not
    /// pile up a backlog without end. The age check on receive still decides what runs.
    /// </summary>
    public static readonly TimeSpan StreamMaxAge = TimeSpan.FromMinutes(30);

    public static TimeSpan MaxAgeFor(string subject) =>
        subject.Contains("setcoverpositionmessage", StringComparison.Ordinal)
        || subject.Contains("setclimate", StringComparison.Ordinal)
            ? ForTargetStates
            : ForImmediateCommands;

    public static bool IsStale(string subject, DateTimeOffset publishedAt, DateTimeOffset now) =>
        now - publishedAt > MaxAgeFor(subject);
}
