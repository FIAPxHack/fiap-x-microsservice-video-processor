using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoProcessor.Application.Dtos;
using VideoProcessor.Application.Interfaces;
using VideoProcessor.Infrastructure.Configurations;

namespace VideoProcessor.Infrastructure.Messaging;

public class SqsConsumer : IQueueConsumer
{
    private readonly IAmazonSQS _sqsClient;
    private readonly AwsSettings _settings;
    private readonly ILogger<SqsConsumer> _logger;

    public SqsConsumer(IAmazonSQS sqsClient, IOptions<AwsSettings> settings, ILogger<SqsConsumer> logger)
    {
        _sqsClient = sqsClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<(VideoMessageDto Message, string ReceiptHandle)>> ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Conectando na fila: {QueueUrl}", _settings.SqsQueueUrl);

        var request = new ReceiveMessageRequest
        {
            QueueUrl = _settings.SqsQueueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 120
        };

        var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);
        var results = new List<(VideoMessageDto, string)>();

        foreach (var sqsMessage in response?.Messages ?? [])
        {
            try
            {
                var message = ParseMessage(sqsMessage.Body);
                if (message is not null)
                {
                    results.Add((message, sqsMessage.ReceiptHandle));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao parsear mensagem SQS: {Body}", sqsMessage.Body);
            }
        }

        return results;
    }

    private VideoMessageDto? ParseMessage(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Formato S3 Event Notification
        if (root.TryGetProperty("Records", out var records))
        {
            var record = records[0];
            var s3 = record.GetProperty("s3");
            var bucketName = s3.GetProperty("bucket").GetProperty("name").GetString()!;
            var objectKey = s3.GetProperty("object").GetProperty("key").GetString()!;

            // Extrair videoId do path (uploads/{videoId}/arquivo.mp4)
            var segments = objectKey.Split('/');
            var videoId = segments.Length >= 2 ? segments[1] : Guid.NewGuid().ToString();

            _logger.LogInformation("S3 Event recebido: {Bucket}/{Key}, VideoId: {VideoId}", bucketName, objectKey, videoId);

            return new VideoMessageDto
            {
                VideoId = videoId,
                S3Key = objectKey,
                BucketName = bucketName
            };
        }

        // Formato manual (VideoMessageDto direto)
        return JsonSerializer.Deserialize<VideoMessageDto>(body);
    }

    public async Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken)
    {
        await _sqsClient.DeleteMessageAsync(_settings.SqsQueueUrl, receiptHandle, cancellationToken);
    }
}