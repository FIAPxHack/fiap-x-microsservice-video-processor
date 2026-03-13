using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VideoProcessor.Application.Dtos;
using VideoProcessor.Application.Interfaces;
using VideoProcessor.Application.UseCases.ProcessVideo;
using VideoProcessor.Worker.Workers;
using Xunit;

namespace FiapXVideoProcessor.Tests.Infrastructure.Workers;

public class VideoProcessingWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_WithMessage_ShouldProcessAndDelete()
    {
        // Arrange
        var mockQueueConsumer = new Mock<IQueueConsumer>();
        var mockMediator = new Mock<IMediator>();
        var mockLogger = new Mock<ILogger<VideoProcessingWorker>>();

        var message = new VideoMessageDto { VideoId = "v1", S3Key = "uploads/v1/v.mp4", BucketName = "b" };
        var messages = new List<(VideoMessageDto, string)> { (message, "receipt-1") };

        var callCount = 0;
        mockQueueConsumer.Setup(x => x.ReceiveMessagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<(VideoMessageDto, string)>();
            });

        mockMediator.Setup(x => x.Send(It.IsAny<ProcessVideoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(IQueueConsumer))).Returns(mockQueueConsumer.Object);
        serviceProvider.Setup(x => x.GetService(typeof(IMediator))).Returns(mockMediator.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var worker = new VideoProcessingWorker(scopeFactory.Object, mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(1000);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        mockMediator.Verify(x => x.Send(
            It.Is<ProcessVideoCommand>(c => c.Message.VideoId == "v1"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        mockQueueConsumer.Verify(x => x.DeleteMessageAsync("receipt-1", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMessages_ShouldContinueLoop()
    {
        // Arrange
        var mockQueueConsumer = new Mock<IQueueConsumer>();
        var mockMediator = new Mock<IMediator>();
        var mockLogger = new Mock<ILogger<VideoProcessingWorker>>();

        mockQueueConsumer.Setup(x => x.ReceiveMessagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(VideoMessageDto, string)>());

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(IQueueConsumer))).Returns(mockQueueConsumer.Object);
        serviceProvider.Setup(x => x.GetService(typeof(IMediator))).Returns(mockMediator.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var worker = new VideoProcessingWorker(scopeFactory.Object, mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(1500);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        mockMediator.Verify(x => x.Send(It.IsAny<ProcessVideoCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}