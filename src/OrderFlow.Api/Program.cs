using OrderFlow.Api;
using OrderFlow.Api.Endpoints;
using OrderFlow.Application;
using OrderFlow.Infrastructure;
using OrderFlow.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddPresentation();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

// Outermost first: resolve the real client address, then log every request with the final
// status code (after the exception handler has turned exceptions into problem details).
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("OrderFlow API"));
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapOrderEndpoints();

await app.RunAsync();
