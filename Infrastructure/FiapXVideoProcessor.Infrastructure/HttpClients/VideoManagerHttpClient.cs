using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using VideoProcessor.Application.Dtos;
using VideoProcessor.Application.Interfaces;

namespace VideoProcessor.Infrastructure.HttpClients;

public class VideoManagerHttpClient : IVideoManagerClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VideoManagerHttpClient> _logger;

    public VideoManagerHttpClient(HttpClient httpClient, ILogger<VideoManagerHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task UpdateVideoStatusAsync(string videoId, VideoStatusUpdateDto statusUpdate, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Atualizando status do vídeo {VideoId} para {Status}",
            videoId, statusUpdate.Status);

        var response = await _httpClient.PutAsJsonAsync(
            $"/api/videos/{videoId}/status",
            statusUpdate,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}