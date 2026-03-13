namespace VideoProcessor.Application.Dtos;

public class VideoMessageDto
{
    public string VideoId { get; set; } = string.Empty;
    public string S3Key { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}