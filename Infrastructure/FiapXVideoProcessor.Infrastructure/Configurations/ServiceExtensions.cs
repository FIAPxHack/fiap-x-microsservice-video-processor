using Amazon;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using VideoProcessor.Application.Interfaces;
using VideoProcessor.Infrastructure.Cache;
using VideoProcessor.Infrastructure.HttpClients;
using VideoProcessor.Infrastructure.Messaging;
using VideoProcessor.Infrastructure.Processing;
using VideoProcessor.Infrastructure.Storage;

namespace VideoProcessor.Infrastructure.Configurations;

public static class ServiceExtensions
{
    public static void ConfigureInfrastructureApp(this IServiceCollection services, IConfiguration configuration)
    {
        // Settings
        services.Configure<AwsSettings>(configuration.GetSection("Aws"));

        var awsRegion = configuration["Aws:Region"] ?? "us-east-1";

        var serviceUrl = configuration["Aws:ServiceUrl"];

        // AWS Clients
        services.AddSingleton<IAmazonSQS>(_ =>
        {
            var config = new AmazonSQSConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion) };
            if (!string.IsNullOrEmpty(serviceUrl))
                config.ServiceURL = serviceUrl;
            return new AmazonSQSClient(config);
        });

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion),
                ForcePathStyle = true
            };
            if (!string.IsNullOrEmpty(serviceUrl))
                config.ServiceURL = serviceUrl;
            return new AmazonS3Client(config);
        });

        // Redis
        var redisConnection = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "video-processor:";
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // Services
        services.AddScoped<IQueueConsumer, SqsConsumer>();
        services.AddScoped<IStorageService, S3StorageService>();
        services.AddScoped<IVideoProcessingService, FfmpegVideoProcessor>();

        // HTTP Client com Polly
        var videoManagerBaseUrl = configuration["VideoManager:BaseUrl"]
    ?? throw new InvalidOperationException("A configuração 'VideoManager:BaseUrl' é obrigatória.");
        services.AddHttpClient<IVideoManagerClient, VideoManagerHttpClient>(client =>
        {
            client.BaseAddress = new Uri(videoManagerBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddTransientHttpErrorPolicy(p =>
            p.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
    }
}