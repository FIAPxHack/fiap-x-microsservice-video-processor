using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using VideoProcessor.Application.Dtos;
using VideoProcessor.Application.Enums;
using VideoProcessor.Infrastructure.HttpClients;
using Xunit;

namespace FiapXVideoProcessor.Tests.Infrastructure.HttpClients;

public class VideoManagerHttpClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly Mock<ILogger<VideoManagerHttpClient>> _mockLogger;
    private readonly VideoManagerHttpClient _client;

    public VideoManagerHttpClientTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<VideoManagerHttpClient>>();

        var httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        _client = new VideoManagerHttpClient(httpClient, _mockLogger.Object);
    }

    [Fact]
    public async Task UpdateVideoStatusAsync_WithSuccessResponse_ShouldNotThrow()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var dto = new VideoStatusUpdateDto
        {
            Status = (int)VideoProcessingStatus.Completed,
            ZipFileName = "vid.zip",
            FrameCount = 100
        };

        // Act
        var act = () => _client.UpdateVideoStatusAsync("video-1", dto, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateVideoStatusAsync_ShouldSendPutToCorrectUrl()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var dto = new VideoStatusUpdateDto { Status = (int)VideoProcessingStatus.Failed, ErrorMessage = "err" };

        // Act
        await _client.UpdateVideoStatusAsync("video-xyz", dto, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Put);
        capturedRequest.RequestUri!.PathAndQuery.Should().Be("/api/videos/video-xyz/status");
    }

    [Fact]
    public async Task UpdateVideoStatusAsync_WithErrorResponse_ShouldThrow()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var dto = new VideoStatusUpdateDto { Status = (int)VideoProcessingStatus.Completed };

        // Act
        var act = () => _client.UpdateVideoStatusAsync("v1", dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}