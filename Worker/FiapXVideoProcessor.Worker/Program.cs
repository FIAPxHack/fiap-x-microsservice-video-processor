using VideoProcessor.Application.Configurations;
using VideoProcessor.Infrastructure.Configurations;
using VideoProcessor.Worker.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configuração das camadas
builder.Services.ConfigureApplicationApp();
builder.Services.ConfigureInfrastructureApp(builder.Configuration);

// Worker como BackgroundService
builder.Services.AddHostedService<VideoProcessingWorker>();

var app = builder.Build();

// Health check
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

await app.RunAsync();