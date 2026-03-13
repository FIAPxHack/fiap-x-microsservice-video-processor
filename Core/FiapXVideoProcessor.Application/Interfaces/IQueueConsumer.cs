using VideoProcessor.Application.Dtos;

namespace VideoProcessor.Application.Interfaces;

public interface IQueueConsumer
{
    Task<IEnumerable<(VideoMessageDto Message, string ReceiptHandle)>> ReceiveMessagesAsync(CancellationToken cancellationToken);
    Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken);
}