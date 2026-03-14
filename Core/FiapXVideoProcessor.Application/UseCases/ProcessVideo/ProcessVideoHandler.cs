using MediatR;
using Microsoft.Extensions.Logging;
using VideoProcessor.Application.Dtos;
using VideoProcessor.Application.Interfaces;
using VideoProcessor.Application.Enums;

namespace VideoProcessor.Application.UseCases.ProcessVideo;

public class ProcessVideoHandler : IRequestHandler<ProcessVideoCommand, bool>
{
    private readonly IStorageService _storageService;
    private readonly IVideoProcessingService _videoProcessingService;
    private readonly IVideoManagerClient _videoManagerClient;
    private readonly ICacheService _cacheService;
    private readonly IVideoProcessingMetrics _metrics;
    private readonly ILogger<ProcessVideoHandler> _logger;

    public ProcessVideoHandler(
        IStorageService storageService,
        IVideoProcessingService videoProcessingService,
        IVideoManagerClient videoManagerClient,
        ICacheService cacheService,
        IVideoProcessingMetrics metrics,
        ILogger<ProcessVideoHandler> logger)
    {
        _storageService = storageService;
        _videoProcessingService = videoProcessingService;
        _videoManagerClient = videoManagerClient;
        _cacheService = cacheService;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessVideoCommand request, CancellationToken cancellationToken)
    {
        var message = request.Message;
        var cacheKey = $"processing:{message.VideoId}";
        var workDir = Path.Combine(Path.GetTempPath(), "video-processor", message.VideoId);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (await _cacheService.ExistsAsync(cacheKey, cancellationToken))
            {
                _logger.LogWarning("Vídeo {VideoId} já está sendo processado. Ignorando duplicata.", message.VideoId);
                return true;
            }

            await _cacheService.SetAsync(cacheKey, "processing", TimeSpan.FromMinutes(30), cancellationToken);

            Directory.CreateDirectory(workDir);

            _logger.LogInformation("Processando vídeo {VideoId} do bucket {Bucket}/{Key}",
                message.VideoId, message.BucketName, message.S3Key);

            // 1. Download do vídeo do S3
            var videoFileName = Path.GetFileName(message.S3Key);
            var localVideoPath = Path.Combine(workDir, videoFileName);
            await _storageService.DownloadFileAsync(message.BucketName, message.S3Key, localVideoPath, cancellationToken);

            // 2. Extrair frames com FFmpeg
            var framesDir = Path.Combine(workDir, "frames");
            Directory.CreateDirectory(framesDir);
            await _videoProcessingService.ExtractFramesAsync(localVideoPath, framesDir, cancellationToken);

            // 3. Contar frames extraídos
            var frameCount = Directory.GetFiles(framesDir, "*.png").Length;

            // 4. Criar ZIP dos frames
            var zipFileName = $"{message.VideoId}.zip";
            var zipPath = Path.Combine(workDir, zipFileName);
            await _videoProcessingService.CreateZipAsync(framesDir, zipPath, cancellationToken);

            // 5. Upload do ZIP para o S3 (path alinhado com Video Manager: outputs/{videoId}/{zipFileName})
            var zipS3Key = $"outputs/{message.VideoId}/{zipFileName}";
            await _storageService.UploadFileAsync(message.BucketName, zipS3Key, zipPath, cancellationToken);

            // 6. Callback para o Video Manager — sucesso
            await _videoManagerClient.UpdateVideoStatusAsync(message.VideoId, new VideoStatusUpdateDto
            {
                Status = (int)VideoProcessingStatus.Completed,
                ZipFileName = zipFileName,
                FrameCount = frameCount
            }, cancellationToken);

            await _cacheService.SetAsync(cacheKey, "completed", TimeSpan.FromHours(24), cancellationToken);

            stopwatch.Stop();
            _metrics.RecordDuration(stopwatch.Elapsed.TotalSeconds, "success");
            _metrics.IncrementProcessed("success");

            _logger.LogInformation("Vídeo {VideoId} processado com sucesso. ZIP: {ZipKey}, Frames: {FrameCount}, Duração: {Duration:F2}s",
                message.VideoId, zipS3Key, frameCount, stopwatch.Elapsed.TotalSeconds);
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Falha ao processar vídeo {VideoId}", message.VideoId);

            _metrics.RecordDuration(stopwatch.Elapsed.TotalSeconds, "failure");
            _metrics.IncrementProcessed("failure");

            await _cacheService.RemoveAsync(cacheKey, cancellationToken);

            try
            {
                await _videoManagerClient.UpdateVideoStatusAsync(message.VideoId, new VideoStatusUpdateDto
                {
                    Status = (int)VideoProcessingStatus.Failed,
                    ErrorMessage = ex.Message
                }, cancellationToken);
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "Falha ao notificar Video Manager sobre erro do vídeo {VideoId}", message.VideoId);
            }

            return false;
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                try { Directory.Delete(workDir, true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Falha ao limpar diretório temporário {WorkDir}", workDir); }
            }
        }
    }
}