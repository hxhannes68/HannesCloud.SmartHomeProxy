using Amazon.Runtime;
using Amazon.SQS;
using HannesCloud.SmartHomeProxy;
using HannesCloud.SmartHomeProxy.Cloud;
using HannesCloud.SmartHomeProxy.Consumers;
using HannesCloud.SmartHomeProxy.HomeAssistant;
using MassTransit;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.Configure<ServiceBusOptions>(
    builder.Configuration.GetSection("ServiceBus"));

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

var serviceBus = builder.Configuration.GetSection("ServiceBus").Get<ServiceBusOptions>()!;
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SetClimateTemperatureConsumer>();
    x.AddConsumer<SetClimateHvacModeConsumer>();
    x.UsingAmazonSqs((context, cfg) =>
    {
        cfg.Host("eu-central-1", h =>
        {
            h.AccessKey(serviceBus.AccessKey);
            h.SecretKey(serviceBus.AccessSecret);
        });
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddSingleton<HomeAssistantWebSocketClient>();
builder.Services.AddHostedService<Worker>();

try
{
    Log.Information("Starting HannesCloud SmartHome Proxy");
    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
