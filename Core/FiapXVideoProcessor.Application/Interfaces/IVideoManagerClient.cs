using VideoProcessor.Application.Dtos;

namespace VideoProcessor.Application.Interfaces;

public interface IVideoManagerClient
{
    Task UpdateVideoStatusAsync(string videoId, VideoStatusUpdateDto statusUpdate, CancellationToken cancellationToken);
}