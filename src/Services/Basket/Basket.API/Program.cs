using Basket.API.GrpcServices;
using Basket.API.Repositories;
using Discount.Grpc.Protos;
using MassTransit;
using Serilog;
using Common.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using StackExchange.Redis;

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
    otelBuilder.AddGrpcClientInstrumentation();
    otelBuilder.AddAspNetCoreInstrumentation();
    otelBuilder.AddMassTransitInstrumentation();
    otelBuilder.Configure((provider, b) => {
        var connectionMultiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
        b.AddRedisInstrumentation(connectionMultiplexer);
    });
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

// Redis Configuration
builder.Services.AddStackExchangeRedisCache(options =>
 {
     options.Configuration = builder.Configuration.GetValue<string>("CacheSettings:ConnectionString");
     //  options.InstanceName = "SampleInstance";
 });

// General Configuration
builder.Services.AddScoped<IBasketRepository, BasketRepository>();
builder.Services.AddAutoMapper(typeof(Program));

// Grpc Configuration
builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>
    (o => o.Address = new Uri(builder.Configuration["GrpcSettings:DiscountUrl"]));
builder.Services.AddScoped<DiscountGrpcService>();

// MassTransit-RabbitMQ Configuration
builder.Services.AddMassTransit(config =>
{
    config.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["EventBusSettings:HostAddress"]);
    });
});
// TODO this is no longer required in the new version
// refactor it
// builder.Services.AddMassTransitHostedService();

builder.Services.AddHealthChecks()
  .AddRedis(
      builder.Configuration["CacheSettings:ConnectionString"],
      "Baskset Redis Health",
      HealthStatus.Degraded);
  // .AddRabbitMQ(
  //     builder.Configuration["CacheSettings:ConnectionString"],
  //     new RabbitMQ.Client.SslOption() { Enabled = false },
  //     "Baskset RabbitMQ Health",
  //     HealthStatus.Degraded);
  // .AddRabbitMQ(
  //     rabbitConnectionString: builder.Configuration["EventBusSettings:HostAddress"]);

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
