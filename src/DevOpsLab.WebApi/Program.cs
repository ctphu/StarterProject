using DevOpsLab.Application.Products;
using DevOpsLab.Infrastructure;
using DevOpsLab.Infrastructure.Persistence;
using DevOpsLab.WebApi.Endpoints;
using DevOpsLab.WebApi.Extensions;
using DevOpsLab.WebApi.HealthChecks;
using DevOpsLab.WebApi.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureSerilog();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ProductService>();
builder.Services.AddApplicationHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Applies migrations and seed data automatically outside Production.
// From Session 10 onwards, migrations are promoted through the pipeline instead.
if (!app.Environment.IsProduction())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    await dbContext.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(dbContext, logger);
}

app.UseApplicationExceptionHandling();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapApplicationHealthChecks();
app.MapProductEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    service = "DevOpsLab.WebApi",
    environment = app.Environment.EnvironmentName,
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    docs = "/swagger",
    liveness = "/healthz/live",
    readiness = "/healthz/ready"
})).ExcludeFromDescription();

app.Run();

/// <summary>Exposed so that WebApplicationFactory can bootstrap the API in integration tests.</summary>
public partial class Program;
