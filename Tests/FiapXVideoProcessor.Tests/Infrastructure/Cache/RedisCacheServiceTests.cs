using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using VideoProcessor.Infrastructure.Cache;
using Xunit;

namespace FiapXVideoProcessor.Tests.Infrastructure.Cache;

public class RedisCacheServiceTests
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly RedisCacheService _service;

    public RedisCacheServiceTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _service = new RedisCacheService(_mockCache.Object);
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        _mockCache.Setup(x => x.GetAsync("test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("value"));

        // Act
        var result = await _service.ExistsAsync("test-key");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _mockCache.Setup(x => x.GetAsync("missing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _service.ExistsAsync("missing-key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_WithExpiration_ShouldCallCacheWithOptions()
    {
        // Act
        await _service.SetAsync("key", "value", TimeSpan.FromMinutes(30));

        // Assert
        _mockCache.Verify(x => x.SetAsync(
            "key",
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(30)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithoutExpiration_ShouldCallCacheWithoutExpiration()
    {
        // Act
        await _service.SetAsync("key", "value");

        // Assert
        _mockCache.Verify(x => x.SetAsync(
            "key",
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_ShouldCallCacheRemove()
    {
        // Act
        await _service.RemoveAsync("key-to-remove");

        // Assert
        _mockCache.Verify(x => x.RemoveAsync("key-to-remove", It.IsAny<CancellationToken>()), Times.Once);
    }
}