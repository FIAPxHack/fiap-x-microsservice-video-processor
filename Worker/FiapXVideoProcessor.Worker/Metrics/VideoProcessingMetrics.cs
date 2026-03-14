using System.Diagnostics.CodeAnalysis;
using Prometheus;
using VideoProcessor.Application.Interfaces;

namespace VideoProcessor.Worker.Metrics;

[ExcludeFromCodeCoverage]
public sealed class VideoProcessingMetrics : IVideoProcessingMetrics
{
    private readonly Counter _videosProcessed;
    private readonly Histogram _processingDuration;

    public VideoProcessingMetrics()
    {
        _videosProcessed = Prometheus.Metrics.CreateCounter(
            "video_processor_videos_processed_total",
            "Total de vídeos processados",
            new CounterConfiguration
            {
                LabelNames = ["status"]
            });

        _processingDuration = Prometheus.Metrics.CreateHistogram(
            "video_processor_duration_seconds",
            "Duração do processamento de vídeo em segundos",
            new HistogramConfiguration
            {
                LabelNames = ["status"],
                Buckets = [5, 10, 30, 60, 120, 300, 600]
            });
    }

    public void IncrementProcessed(string status) =>
        _videosProcessed.WithLabels(status).Inc();

    public void RecordDuration(double seconds, string status) =>
        _processingDuration.WithLabels(status).Observe(seconds);
}

