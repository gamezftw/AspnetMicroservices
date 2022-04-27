using Discount.Grpc.Services;
using Discount.Grpc.Repositories;
using Discount.Grpc.Extensions;
using Serilog;
using Common.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(SeriLogger.Configure);

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
    otelBuilder.AddMassTransitInstrumentation();
    otelBuilder.AddSqlClientInstrumentation(); // TODO this uses SqlClient instrumentation, find out what is Npgsql.OpenTelemetry
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
    otelBuilder.AddOtlpExporter(options => options.Endpoint = new Uri(builder.Configuration["OpenTelemetry:CollectorUrl"]));
});

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();

builder.Services.AddScoped<IDiscountRepository, DiscountRepository>();
builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddHealthChecks()
  .AddNpgSql(builder.Configuration["DatabaseSettings:ConnectionString"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<DiscountService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.MigrateDatabase<Program>();

app.MapHealthChecks("/hc", new HealthCheckOptions()
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
