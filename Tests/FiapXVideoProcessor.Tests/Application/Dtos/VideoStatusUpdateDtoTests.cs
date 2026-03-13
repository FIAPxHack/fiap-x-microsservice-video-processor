using FluentAssertions;
using VideoProcessor.Application.Dtos;
using VideoProcessor.Application.Enums;
using Xunit;

namespace FiapXVideoProcessor.Tests.Application.Dtos;

public class VideoStatusUpdateDtoTests
{
    [Fact]
    public void VideoStatusUpdateDto_ShouldSetAllProperties()
    {
        // Arrange & Act
        var dto = new VideoStatusUpdateDto
        {
            Status = (int)VideoProcessingStatus.Completed,
            ZipFileName = "video-123.zip",
            FrameCount = 120,
            ErrorMessage = null
        };

        // Assert
        dto.Status.Should().Be((int)VideoProcessingStatus.Completed);
        dto.ZipFileName.Should().Be("video-123.zip");
        dto.FrameCount.Should().Be(120);
        dto.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void VideoStatusUpdateDto_ForFailedStatus_ShouldHaveErrorMessage()
    {
        // Arrange & Act
        var dto = new VideoStatusUpdateDto
        {
            Status = (int)VideoProcessingStatus.Failed,
            ErrorMessage = "FFmpeg processing failed"
        };

        // Assert
        dto.Status.Should().Be((int)VideoProcessingStatus.Failed);
        dto.ErrorMessage.Should().Be("FFmpeg processing failed");
        dto.ZipFileName.Should().BeNull();
        dto.FrameCount.Should().Be(0);
    }

    [Fact]
    public void VideoMessageDto_ShouldSetAllProperties()
    {
        // Arrange & Act
        var dto = new VideoMessageDto
        {
            VideoId = "video-123",
            S3Key = "uploads/video-123/video.mp4",
            BucketName = "test-bucket"
        };

        // Assert
        dto.VideoId.Should().Be("video-123");
        dto.S3Key.Should().Be("uploads/video-123/video.mp4");
        dto.BucketName.Should().Be("test-bucket");
    }

    [Fact]
    public void VideoMessageDto_DefaultValues_ShouldBeEmpty()
    {
        // Arrange & Act
        var dto = new VideoMessageDto();

        // Assert
        dto.VideoId.Should().BeEmpty();
        dto.S3Key.Should().BeEmpty();
        dto.BucketName.Should().BeEmpty();
    }
}