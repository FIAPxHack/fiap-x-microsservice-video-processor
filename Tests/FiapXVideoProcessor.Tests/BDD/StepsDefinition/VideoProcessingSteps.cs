using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using VideoProcessor.Application.Dtos;
using VideoProcessor.Application.Enums;
using VideoProcessor.Application.Interfaces;
using VideoProcessor.Application.UseCases.ProcessVideo;
using TechTalk.SpecFlow;

namespace FiapXVideoProcessor.Tests.BDD.StepDefinitions;

[Binding]
public class VideoProcessingSteps
{
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<IVideoProcessingService> _mockVideoProcessingService;
    private readonly Mock<IVideoManagerClient> _mockVideoManagerClient;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ILogger<ProcessVideoHandler>> _mockLogger;

    private ProcessVideoHandler _handler = null!;
    private VideoMessageDto _message = null!;
    private bool _result;
    private bool _cacheExists;
    private string? _downloadError;
    private string? _extractionError;
    private bool _callbackShouldFail;

    public VideoProcessingSteps()
    {
        _mockStorageService = new Mock<IStorageService>();
        _mockVideoProcessingService = new Mock<IVideoProcessingService>();
        _mockVideoManagerClient = new Mock<IVideoManagerClient>();
        _mockCacheService = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<ProcessVideoHandler>>();
    }

    [Given(@"que recebi uma mensagem para processar o vídeo ""(.*)"" do bucket ""(.*)""")]
    public void DadoQueRecebiUmaMensagemParaProcessarOVideoNoBucket(string videoId, string bucket)
    {
        _message = new VideoMessageDto
        {
            VideoId = videoId,
            S3Key = $"uploads/{videoId}/video.mp4",
            BucketName = bucket
        };
    }

    [Given(@"o vídeo não está sendo processado atualmente")]
    public void DadoOVideoNaoEstaSendoProcessadoAtualmente()
    {
        _cacheExists = false;
    }

    [Given(@"o vídeo já está sendo processado")]
    public void DadoOVideoJaEstaSendoProcessado()
    {
        _cacheExists = true;
    }

    [Given(@"o download do S3 vai falhar com erro ""(.*)""")]
    public void DadoODownloadDoS3VaiFalharComErro(string error)
    {
        _downloadError = error;
    }

    [Given(@"a extração de frames vai falhar com erro ""(.*)""")]
    public void DadoAExtracaoDeFramesVaiFalharComErro(string error)
    {
        _extractionError = error;
    }

    [Given(@"o callback ao Video Manager vai falhar")]
    public void DadoOCallbackAoVideoManagerVaiFalhar()
    {
        _callbackShouldFail = true;
    }

    [When(@"o processamento é executado")]
    public async Task QuandoOProcessamentoEExecutado()
    {
        SetupMocks();
        _handler = new ProcessVideoHandler(
            _mockStorageService.Object,
            _mockVideoProcessingService.Object,
            _mockVideoManagerClient.Object,
            _mockCacheService.Object,
            _mockLogger.Object);

        var command = new ProcessVideoCommand(_message);
        _result = await _handler.Handle(command, CancellationToken.None);
    }

    [Then(@"o resultado deve ser sucesso")]
    public void EntaoOResultadoDeveSerSucesso()
    {
        _result.Should().BeTrue();
    }

    [Then(@"o resultado deve ser falha")]
    public void EntaoOResultadoDeveSerFalha()
    {
        _result.Should().BeFalse();
    }

    [Then(@"o status enviado ao Video Manager deve ser ""(.*)""")]
    public void EntaoOStatusEnviadoAoVideoManagerDeveSer(string status)
    {
        var expectedStatus = (int)Enum.Parse<VideoProcessingStatus>(status);

        _mockVideoManagerClient.Verify(x => x.UpdateVideoStatusAsync(
            _message.VideoId,
            It.Is<VideoStatusUpdateDto>(dto => dto.Status == expectedStatus),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Then(@"o vídeo não deve ser baixado do S3")]
    public void EntaoOVideoNaoDeveSerBaixadoDoS3()
    {
        _mockStorageService.Verify(x => x.DownloadFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Then(@"a chave de cache deve ser removida")]
    public void EntaoAChaveDeCacheDeveSerRemovida()
    {
        _mockCacheService.Verify(x => x.RemoveAsync(
            $"processing:{_message.VideoId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Then(@"o ZIP deve ser enviado para o caminho ""(.*)"" no S3")]
    public void EntaoOZipDeveSerEnviadoParaOCaminhoNoS3(string expectedKey)
    {
        _mockStorageService.Verify(x => x.UploadFileAsync(
            _message.BucketName,
            expectedKey,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupMocks()
    {
        _mockCacheService.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cacheExists);

        if (_downloadError != null)
        {
            _mockStorageService.Setup(x => x.DownloadFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException(_downloadError));
        }
        else
        {
            _mockStorageService.Setup(x => x.DownloadFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string _, string localPath, CancellationToken _) => localPath);
        }

        if (_extractionError != null)
        {
            _mockVideoProcessingService.Setup(x => x.ExtractFramesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(_extractionError));
        }
        else
        {
            _mockVideoProcessingService.Setup(x => x.ExtractFramesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string outputDir, CancellationToken _) => outputDir);
        }

        _mockVideoProcessingService.Setup(x => x.CreateZipAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string zipPath, CancellationToken _) => zipPath);

        _mockStorageService.Setup(x => x.UploadFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, string _, CancellationToken _) => key);

        if (_callbackShouldFail)
        {
            _mockVideoManagerClient.Setup(x => x.UpdateVideoStatusAsync(
                It.IsAny<string>(), It.IsAny<VideoStatusUpdateDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("callback failed"));
        }
    }
}