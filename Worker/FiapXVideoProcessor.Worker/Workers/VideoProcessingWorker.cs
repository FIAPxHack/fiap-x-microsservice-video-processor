using MediatR;
using VideoProcessor.Application.Interfaces;
using VideoProcessor.Application.UseCases.ProcessVideo;

namespace VideoProcessor.Worker.Workers;

public class VideoProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VideoProcessingWorker> _logger;

    public VideoProcessingWorker(IServiceScopeFactory scopeFactory, ILogger<VideoProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Video Processing Worker iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queueConsumer = scope.ServiceProvider.GetRequiredService<IQueueConsumer>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var messages = await queueConsumer.ReceiveMessagesAsync(stoppingToken);

                foreach (var (message, receiptHandle) in messages)
                {
                    _logger.LogInformation("Mensagem recebida para vídeo {VideoId}", message.VideoId);

                    var command = new ProcessVideoCommand(message);
                    var success = await mediator.Send(command, stoppingToken);

                    // Remove da fila independente de sucesso/falha (falha já fez callback)
                    await queueConsumer.DeleteMessageAsync(receiptHandle, stoppingToken);

                    _logger.LogInformation("Vídeo {VideoId} processado. Sucesso: {Success}", message.VideoId, success);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no loop de processamento. Aguardando antes de retry...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Video Processing Worker encerrado");
    }
}