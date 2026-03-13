namespace VideoProcessor.Application.Interfaces;

public interface IVideoProcessingService
{
    Task<string> ExtractFramesAsync(string videoFilePath, string outputDirectory, CancellationToken cancellationToken);
    Task<string> CreateZipAsync(string framesDirectory, string outputZipPath, CancellationToken cancellationToken);
}