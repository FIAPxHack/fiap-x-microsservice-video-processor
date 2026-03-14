using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using VideoProcessor.Application.Dtos;
using VideoProcessor.Application.Enums;
using VideoProcessor.Application.Interfaces;
using VideoProcessor.Application.UseCases.ProcessVideo;
using Xunit;

namespace FiapXVideoProcessor.Tests.Application.UseCases;

public class ProcessVideoHandlerTests
{
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<IVideoProcessingService> _mockVideoProcessingService;
    private readonly Mock<IVideoManagerClient> _mockVideoManagerClient;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IVideoProcessingMetrics> _mockMetrics;
    private readonly Mock<ILogger<ProcessVideoHandler>> _mockLogger;
    private readonly ProcessVideoHandler _handler;

    public ProcessVideoHandlerTests()
    {
        _mockStorageService = new Mock<IStorageService>();
        _mockVideoProcessingService = new Mock<IVideoProcessingService>();
        _mockVideoManagerClient = new Mock<IVideoManagerClient>();
        _mockCacheService = new Mock<ICacheService>();
        _mockMetrics = new Mock<IVideoProcessingMetrics>();
        _mockLogger = new Mock<ILogger<ProcessVideoHandler>>();

        _handler = new ProcessVideoHandler(
            _mockStorageService.Object,
            _mockVideoProcessingService.Object,
            _mockVideoManagerClient.Object,
            _mockCacheService.Object,
            _mockMetrics.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidMessage_ShouldReturnTrue()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        SetupSuccessfulProcessing(message);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldDownloadVideoFromS3()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        SetupSuccessfulProcessing(message);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockStorageService.Verify(x => x.DownloadFileAsync(
            message.BucketName,
            message.S3Key,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldExtractFramesFromVideo()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        SetupSuccessfulProcessing(message);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockVideoProcessingService.Verify(x => x.ExtractFramesAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateZipFromFrames()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        SetupSuccessfulProcessing(message);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockVideoProcessingService.Verify(x => x.CreateZipAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUploadZipToS3WithCorrectKey()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        SetupSuccessfulProcessing(message);

        var expectedZipS3Key = $"outputs/{message.VideoId}/{message.VideoId}.zip";

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockStorageService.Verify(x => x.UploadFileAsync(
            message.BucketName,
            expectedZipS3Key,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCallbackVideoManagerWithCompletedStatus()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        SetupSuccessfulProcessing(message);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockVideoManagerClient.Verify(x => x.UpdateVideoStatusAsync(
            message.VideoId,
            It.Is<VideoStatusUpdateDto>(dto =>
                dto.Status == (int)VideoProcessingStatus.Completed &&
                dto.ZipFileName == $"{message.VideoId}.zip"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDuplicateMessage_ShouldSkipAndReturnTrue()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);

        _mockCacheService.Setup(x => x.ExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _mockStorageService.Verify(x => x.DownloadFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDuplicateMessage_ShouldNotCallVideoManager()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);

        _mockCacheService.Setup(x => x.ExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockVideoManagerClient.Verify(x => x.UpdateVideoStatusAsync(
            It.IsAny<string>(), It.IsAny<VideoStatusUpdateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSetCacheKeyBeforeProcessing()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        SetupSuccessfulProcessing(message);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCacheService.Verify(x => x.SetAsync(
            $"processing:{message.VideoId}",
            "processing",
            TimeSpan.FromMinutes(30),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OnSuccess_ShouldUpdateCacheToCompleted()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        SetupSuccessfulProcessing(message);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCacheService.Verify(x => x.SetAsync(
            $"processing:{message.VideoId}",
            "completed",
            TimeSpan.FromHours(24),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDownloadFails_ShouldReturnFalse()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);

        _mockCacheService.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockStorageService.Setup(x => x.DownloadFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("S3 download failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenProcessingFails_ShouldCallbackWithFailedStatus()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);
        var errorMessage = "FFmpeg processing failed";

        _mockCacheService.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockStorageService.Setup(x => x.DownloadFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(errorMessage));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockVideoManagerClient.Verify(x => x.UpdateVideoStatusAsync(
            message.VideoId,
            It.Is<VideoStatusUpdateDto>(dto =>
                dto.Status == (int)VideoProcessingStatus.Failed &&
                dto.ErrorMessage == errorMessage),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProcessingFails_ShouldRemoveCacheKey()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);

        _mockCacheService.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockVideoProcessingService.Setup(x => x.ExtractFramesAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("error"));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCacheService.Verify(x => x.RemoveAsync(
            $"processing:{message.VideoId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCallbackFails_ShouldStillReturnFalse()
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);

        _mockCacheService.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockStorageService.Setup(x => x.DownloadFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("download error"));
        _mockVideoManagerClient.Setup(x => x.UpdateVideoStatusAsync(
            It.IsAny<string>(), It.IsAny<VideoStatusUpdateDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("callback failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(VideoProcessingStatus.Completed)]
    [InlineData(VideoProcessingStatus.Failed)]
    public async Task Handle_ShouldSendCorrectStatusToVideoManager(VideoProcessingStatus expectedStatus)
    {
        // Arrange
        var message = CreateTestMessage();
        var command = new ProcessVideoCommand(message);

        _mockCacheService.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        if (expectedStatus == VideoProcessingStatus.Completed)
        {
            SetupSuccessfulProcessing(message);
        }
        else
        {
            _mockStorageService.Setup(x => x.DownloadFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("error"));
        }

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockVideoManagerClient.Verify(x => x.UpdateVideoStatusAsync(
            message.VideoId,
            It.Is<VideoStatusUpdateDto>(dto => dto.Status == (int)expectedStatus),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static VideoMessageDto CreateTestMessage() => new()
    {
        VideoId = "video-123",
        S3Key = "uploads/video-123/video.mp4",
        BucketName = "test-bucket"
    };

    private void SetupSuccessfulProcessing(VideoMessageDto message)
    {
        _mockCacheService.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockStorageService.Setup(x => x.DownloadFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string localPath, CancellationToken _) => localPath);

        _mockVideoProcessingService.Setup(x => x.ExtractFramesAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string outputDir, CancellationToken _) => outputDir);

        _mockVideoProcessingService.Setup(x => x.CreateZipAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string zipPath, CancellationToken _) => zipPath);

        _mockStorageService.Setup(x => x.UploadFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, string _, CancellationToken _) => key);
    }
}