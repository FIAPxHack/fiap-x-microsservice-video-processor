using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VideoProcessor.Infrastructure.Configurations;
using VideoProcessor.Infrastructure.Messaging;
using Xunit;

namespace FiapXVideoProcessor.Tests.Infrastructure.Messaging;

public class SqsConsumerTests
{
    private readonly Mock<IAmazonSQS> _mockSqsClient;
    private readonly Mock<ILogger<SqsConsumer>> _mockLogger;
    private readonly SqsConsumer _consumer;

    public SqsConsumerTests()
    {
        _mockSqsClient = new Mock<IAmazonSQS>();
        _mockLogger = new Mock<ILogger<SqsConsumer>>();
        var settings = Options.Create(new AwsSettings { SqsQueueUrl = "https://sqs.us-east-1.amazonaws.com/123/test-queue" });
        _consumer = new SqsConsumer(_mockSqsClient.Object, settings, _mockLogger.Object);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithS3EventNotification_ShouldParseCorrectly()
    {
        // Arrange
        var s3Event = new
        {
            Records = new[]
            {
                new
                {
                    s3 = new
                    {
                        bucket = new { name = "my-bucket" },
                        @object = new { key = "uploads/video-123/video.mp4" }
                    }
                }
            }
        };

        var response = new ReceiveMessageResponse
        {
            Messages = new List<Message>
            {
                new() { Body = JsonSerializer.Serialize(s3Event), ReceiptHandle = "handle-1" }
            }
        };

        _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var results = (await _consumer.ReceiveMessagesAsync(CancellationToken.None)).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Message.VideoId.Should().Be("video-123");
        results[0].Message.BucketName.Should().Be("my-bucket");
        results[0].Message.S3Key.Should().Be("uploads/video-123/video.mp4");
        results[0].ReceiptHandle.Should().Be("handle-1");
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithManualFormat_ShouldParseCorrectly()
    {
        // Arrange
        var manualMessage = new { VideoId = "vid-456", S3Key = "uploads/vid-456/clip.mp4", BucketName = "bucket-2" };
        var response = new ReceiveMessageResponse
        {
            Messages = new List<Message>
            {
                new() { Body = JsonSerializer.Serialize(manualMessage), ReceiptHandle = "handle-2" }
            }
        };

        _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var results = (await _consumer.ReceiveMessagesAsync(CancellationToken.None)).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Message.VideoId.Should().Be("vid-456");
        results[0].Message.BucketName.Should().Be("bucket-2");
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithNoMessages_ShouldReturnEmpty()
    {
        // Arrange
        var response = new ReceiveMessageResponse { Messages = new List<Message>() };
        _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var results = await _consumer.ReceiveMessagesAsync(CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithInvalidJson_ShouldSkipMessage()
    {
        // Arrange
        var response = new ReceiveMessageResponse
        {
            Messages = new List<Message>
            {
                new() { Body = "invalid-json{{{", ReceiptHandle = "handle-bad" }
            }
        };

        _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var results = await _consumer.ReceiveMessagesAsync(CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteMessageAsync_ShouldCallSqsClient()
    {
        // Arrange
        var receiptHandle = "handle-to-delete";

        // Act
        await _consumer.DeleteMessageAsync(receiptHandle, CancellationToken.None);

        // Assert
        _mockSqsClient.Verify(x => x.DeleteMessageAsync(
            "https://sqs.us-east-1.amazonaws.com/123/test-queue",
            receiptHandle,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithS3Event_SingleSegmentKey_ShouldGenerateVideoId()
    {
        // Arrange
        var s3Event = new
        {
            Records = new[]
            {
                new
                {
                    s3 = new
                    {
                        bucket = new { name = "bucket" },
                        @object = new { key = "video.mp4" }
                    }
                }
            }
        };

        var response = new ReceiveMessageResponse
        {
            Messages = new List<Message>
            {
                new() { Body = JsonSerializer.Serialize(s3Event), ReceiptHandle = "h" }
            }
        };

        _mockSqsClient.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var results = (await _consumer.ReceiveMessagesAsync(CancellationToken.None)).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Message.VideoId.Should().NotBeNullOrEmpty();
    }
}