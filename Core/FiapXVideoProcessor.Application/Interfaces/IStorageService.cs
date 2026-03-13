namespace VideoProcessor.Application.Interfaces;

public interface IStorageService
{
    Task<string> DownloadFileAsync(string bucketName, string key, string localPath, CancellationToken cancellationToken);
    Task<string> UploadFileAsync(string bucketName, string key, string filePath, CancellationToken cancellationToken);
}