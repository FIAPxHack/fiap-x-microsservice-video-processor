namespace VideoProcessor.Application.Interfaces;

/// <summary>
/// Abstração das métricas de processamento de vídeo.
/// A implementação concreta fica no Worker (com prometheus-net).
/// </summary>
public interface IVideoProcessingMetrics
{
    void IncrementProcessed(string status);
    void RecordDuration(double seconds, string status);
}

