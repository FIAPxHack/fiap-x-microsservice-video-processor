using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using VideoProcessor.Application.Interfaces;

namespace VideoProcessor.Infrastructure.Processing;

public class FfmpegVideoProcessor : IVideoProcessingService
{
    private readonly ILogger<FfmpegVideoProcessor> _logger;

    public FfmpegVideoProcessor(ILogger<FfmpegVideoProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractFramesAsync(string videoFilePath, string outputDirectory, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Extraindo frames de {VideoFile} para {OutputDir}", videoFilePath, outputDirectory);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{videoFilePath}\" -vf fps=1 \"{Path.Combine(outputDirectory, "frame_%04d.png")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg falhou com código {process.ExitCode}: {stderr}");
        }

        var frameCount = Directory.GetFiles(outputDirectory, "*.png").Length;
        _logger.LogInformation("{FrameCount} frames extraídos", frameCount);

        if (frameCount == 0)
        {
            throw new InvalidOperationException("Nenhum frame foi extraído do vídeo");
        }

        return outputDirectory;
    }

    public Task<string> CreateZipAsync(string framesDirectory, string outputZipPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Criando ZIP em {ZipPath} a partir de {FramesDir}", outputZipPath, framesDirectory);

        if (File.Exists(outputZipPath))
        {
            File.Delete(outputZipPath);
        }

        ZipFile.CreateFromDirectory(framesDirectory, outputZipPath, CompressionLevel.Optimal, false);

        var zipSize = new FileInfo(outputZipPath).Length;
        _logger.LogInformation("ZIP criado: {Size} bytes", zipSize);

        return Task.FromResult(outputZipPath);
    }
}