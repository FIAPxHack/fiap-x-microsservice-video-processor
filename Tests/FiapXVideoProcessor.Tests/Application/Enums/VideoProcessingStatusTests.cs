using FluentAssertions;
using VideoProcessor.Application.Enums;
using Xunit;

namespace FiapXVideoProcessor.Tests.Application.Enums;

public class VideoProcessingStatusTests
{
    [Fact]
    public void VideoProcessingStatus_ShouldHaveFourValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<VideoProcessingStatus>();

        // Assert
        values.Should().HaveCount(4);
    }

    [Theory]
    [InlineData(VideoProcessingStatus.Pending, 0)]
    [InlineData(VideoProcessingStatus.Processing, 1)]
    [InlineData(VideoProcessingStatus.Completed, 2)]
    [InlineData(VideoProcessingStatus.Failed, 3)]
    public void VideoProcessingStatus_ShouldHaveCorrectIntValues(VideoProcessingStatus status, int expectedValue)
    {
        // Assert
        ((int)status).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0, VideoProcessingStatus.Pending)]
    [InlineData(1, VideoProcessingStatus.Processing)]
    [InlineData(2, VideoProcessingStatus.Completed)]
    [InlineData(3, VideoProcessingStatus.Failed)]
    public void VideoProcessingStatus_ShouldCastFromInt(int value, VideoProcessingStatus expectedStatus)
    {
        // Act
        var status = (VideoProcessingStatus)value;

        // Assert
        status.Should().Be(expectedStatus);
    }
}