namespace VideoProcessor.Application.Dtos;

public class VideoStatusUpdateDto
{
    public int Status { get; set; }
    public string? ZipFileName { get; set; }
    public int FrameCount { get; set; }
    public string? ErrorMessage { get; set; }
}