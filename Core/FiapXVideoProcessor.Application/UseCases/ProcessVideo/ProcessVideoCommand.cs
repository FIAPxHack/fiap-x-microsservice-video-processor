using MediatR;
using VideoProcessor.Application.Dtos;

namespace VideoProcessor.Application.UseCases.ProcessVideo;

public sealed record ProcessVideoCommand(VideoMessageDto Message) : IRequest<bool>;