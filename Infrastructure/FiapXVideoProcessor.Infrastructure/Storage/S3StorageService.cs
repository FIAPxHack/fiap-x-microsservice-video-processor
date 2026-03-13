using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;
using VideoProcessor.Application.Interfaces;

namespace VideoProcessor.Infrastructure.Storage;

public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IAmazonS3 s3Client, ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    public async Task<string> DownloadFileAsync(string bucketName, string key, string localPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Baixando {Bucket}/{Key} para {LocalPath}", bucketName, key, localPath);

        using var transferUtility = new TransferUtility(_s3Client);
        await transferUtility.DownloadAsync(localPath, bucketName, key, cancellationToken);

        return localPath;
    }

    public async Task<string> UploadFileAsync(string bucketName, string key, string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Enviando {FilePath} para {Bucket}/{Key}", filePath, bucketName, key);

        using var transferUtility = new TransferUtility(_s3Client);
        await transferUtility.UploadAsync(filePath, bucketName, key, cancellationToken);

        return key;
    }
}