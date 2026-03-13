namespace VideoProcessor.Infrastructure.Configurations;

public class AwsSettings
{
    public string Region { get; set; } = "us-east-1";
    public string SqsQueueUrl { get; set; } = string.Empty;
}