using Shopping.Aggregator.Services;
using Common.Logging;
using Serilog;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

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

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddTransient<LoggingDelegatingHandler>();

builder.Services.AddHttpClient<ICatalogService, CatalogService>(c =>
    c.BaseAddress = new Uri(builder.Configuration["ApiSettings:CatalogUrl"]))
    .AddHttpMessageHandler<LoggingDelegatingHandler>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddHttpClient<IBasketService, BasketService>(c =>
    c.BaseAddress = new Uri(builder.Configuration["ApiSettings:BasketUrl"]))
    .AddHttpMessageHandler<LoggingDelegatingHandler>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddHttpClient<IOrderService, OrderService>(c =>
    c.BaseAddress = new Uri(builder.Configuration["ApiSettings:OrderingUrl"]))
    .AddHttpMessageHandler<LoggingDelegatingHandler>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddHealthChecks()
  .AddUrlGroup(
      new Uri($"{builder.Configuration["ApiSettings:CatalogUrl"]}/hc"),
      "Catalog.API",
      HealthStatus.Degraded)
  .AddUrlGroup(
      new Uri($"{builder.Configuration["ApiSettings:BasketUrl"]}/hc"),
      "Basket.API",
      HealthStatus.Degraded)
  .AddUrlGroup(
      new Uri($"{builder.Configuration["ApiSettings:OrderingUrl"]}/hc"),
      "Ordering.API",
      HealthStatus.Degraded);

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/hc", new HealthCheckOptions()
    {
      Predicate = _ => true,
      ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    // In this case will wait for
    //  2 ^ 1 = 2 seconds then
    //  2 ^ 2 = 4 seconds then
    //  2 ^ 3 = 8 seconds then
    //  2 ^ 4 = 16 seconds then
    //  2 ^ 5 = 32 seconds

    return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: (retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))),
                onRetry: (exception, retryCount, context) =>
                {
                    Log.Error($"Retry {retryCount} of {context.PolicyKey} at {context.OperationKey}, due to: {exception}.");
                });

}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30)
            );
}
