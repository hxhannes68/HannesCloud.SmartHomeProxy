using HannesCloud.SmartHomeProxy;
using HannesCloud.SmartHomeProxy.Cloud;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using HannesCloud.SmartHomeProxy.LocalSensors;
using Serilog;

// A web host rather than a plain worker only for POST /local/states, which sensors on the
// LAN (the Raspberry Pi) post their readings to.
var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.Configure<HomeAssistantOptions>(
    builder.Configuration.GetSection("HomeAssistant"));

builder.Services.Configure<CloudOptions>(
    builder.Configuration.GetSection("Cloud"));

builder.Services.Configure<NatsOptions>(
    builder.Configuration.GetSection("Nats"));

builder.Services.Configure<LocalSensorOptions>(
    builder.Configuration.GetSection("LocalSensors"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LocalSensorThrottle>();

// HA REST client
builder.Services.AddHttpClient<HomeAssistantRestClient>((_, client) =>
{
    var opts = builder.Configuration.GetSection("HomeAssistant").Get<HomeAssistantOptions>()!;
    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Cloud client — only registered when BaseUrl is configured
var cloudBaseUrl = builder.Configuration["Cloud:BaseUrl"];
if (!string.IsNullOrEmpty(cloudBaseUrl))
{
    builder.Services.AddHttpClient<SmartHomeCloudClient>((_, client) =>
    {
        client.BaseAddress = new Uri(cloudBaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(15);
    });
    builder.Services.AddSingleton<Auth0TokenProvider>();
}
else
{
    Log.Information("Cloud:BaseUrl not configured — running in log-only mode");
}

builder.Services.AddHostedService<NatsConsumerService>();

builder.Services.AddSingleton<HomeAssistantWebSocketClient>();
builder.Services.AddHostedService<Worker>();

try
{
    Log.Information("Starting HannesCloud SmartHome Proxy");
    var app = builder.Build();
    app.MapLocalSensors();
    if (string.IsNullOrEmpty(app.Configuration["LocalSensors:ApiKey"]))
    {
        Log.Warning("LocalSensors:ApiKey not configured — POST /local/states turns every request away");
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
