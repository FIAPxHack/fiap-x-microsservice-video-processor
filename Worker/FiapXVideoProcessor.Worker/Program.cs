using System.Diagnostics.CodeAnalysis;
using Prometheus;
using VideoProcessor.Application.Configurations;
using VideoProcessor.Application.Interfaces;
using VideoProcessor.Infrastructure.Configurations;
using VideoProcessor.Worker.Metrics;
using VideoProcessor.Worker.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configuração das camadas
builder.Services.ConfigureApplicationApp();
builder.Services.ConfigureInfrastructureApp(builder.Configuration);

// Métricas Prometheus (singleton pois os counters/histograms são globais)
builder.Services.AddSingleton<IVideoProcessingMetrics, VideoProcessingMetrics>();

// Worker como BackgroundService
builder.Services.AddHostedService<VideoProcessingWorker>();

var app = builder.Build();

// Health check
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Endpoint de métricas Prometheus (coleta automática: GC, threadpool, etc.)
app.MapMetrics();

await app.RunAsync();

[ExcludeFromCodeCoverage]
public partial class Program { }