using FluentAssertions;
using VideoProcessor.Infrastructure.Configurations;
using Xunit;

namespace FiapXVideoProcessor.Tests.Infrastructure.Configurations;

public class AwsSettingsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Act
        var settings = new AwsSettings();

        // Assert
        settings.Region.Should().Be("us-east-1");
        settings.SqsQueueUrl.Should().BeEmpty();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Act
        var settings = new AwsSettings
        {
            Region = "sa-east-1",
            SqsQueueUrl = "https://sqs.sa-east-1.amazonaws.com/123/my-queue"
        };

        // Assert
        settings.Region.Should().Be("sa-east-1");
        settings.SqsQueueUrl.Should().Be("https://sqs.sa-east-1.amazonaws.com/123/my-queue");
    }
}