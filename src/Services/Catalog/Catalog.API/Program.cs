using Catalog.API.Data;
using Catalog.API.Repositories;
using Serilog;
using Common.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(SeriLogger.Configure);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ICatalogContext, CatalogContext>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

builder.Services.AddHealthChecks()
  .AddMongoDb(
      builder.Configuration.GetValue<string>("DatabaseSettings:ConnectionString"),
      "Catalog MongoDb Health",
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
