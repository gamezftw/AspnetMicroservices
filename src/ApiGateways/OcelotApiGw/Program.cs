using Common.Logging;
using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

// Configure metrics
builder.Services.AddOpenTelemetryMetrics(otelBuilder =>
{
    otelBuilder.AddHttpClientInstrumentation();
    otelBuilder.AddAspNetCoreInstrumentation();
    otelBuilder.AddMeter("MyApplicationMetrics");
    otelBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(builder.Configuration["OpenTelemetry:ResourceName"]));
    otelBuilder.AddOtlpExporter(options => options.Endpoint = new Uri(builder.Configuration["OpenTelemetry:CollectorUrl"]));
});

// Configure tracing
builder.Services.AddOpenTelemetryTracing(otelBuilder =>
{
    otelBuilder.AddHttpClientInstrumentation();
    otelBuilder.AddAspNetCoreInstrumentation();
    otelBuilder.AddSource("MyApplicationActivitySource");
    otelBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(builder.Configuration["OpenTelemetry:ResourceName"]));
    otelBuilder.AddOtlpExporter(options => options.Endpoint = new Uri(builder.Configuration["OpenTelemetry:CollectorUrl"]));
});

// Configure logging
builder.Logging.AddOpenTelemetry(otelBuilder =>
{
    otelBuilder.IncludeFormattedMessage = true;
    otelBuilder.IncludeScopes = true;
    otelBuilder.ParseStateValues = true;
    otelBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(builder.Configuration["OpenTelemetry:ResourceName"]));
});

builder.Host.UseSerilog(SeriLogger.Configure);

builder.Configuration.AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", true, true);

builder.Services.AddOcelot()
    .AddCacheManager(settings => settings.WithDictionaryHandle());

var app = builder.Build();

app.MapHealthChecks("/hc", new HealthCheckOptions()
    {
      Predicate = _ => true,
      ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

app.Run();

app.MapGet("/", () => "Hello World!");

app.UseOcelot().Wait();

app.Run();
