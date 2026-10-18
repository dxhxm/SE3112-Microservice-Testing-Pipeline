using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StreamService.Repositories;
using StreamService.Services;
using Xunit;

namespace StreamService.Tests.Services;

public class WebhookLogPrunerServiceTests
{
    private readonly Mock<IWebhookLogRepository> _webhookLogRepoMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _scopedProviderMock;
    private readonly Mock<IServiceProvider> _rootProviderMock;
    private readonly Mock<ILogger<WebhookLogPrunerService>> _loggerMock;

    public WebhookLogPrunerServiceTests()
    {
        _webhookLogRepoMock = new Mock<IWebhookLogRepository>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _scopedProviderMock = new Mock<IServiceProvider>();
        _rootProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<WebhookLogPrunerService>>();

        // Setup ServiceScope resolution: rootProvider -> scopeFactory -> scope -> scopedProvider -> repo
        _rootProviderMock
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactoryMock.Object);

        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_scopeMock.Object);

        _scopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(_scopedProviderMock.Object);

        _scopedProviderMock
            .Setup(sp => sp.GetService(typeof(IWebhookLogRepository)))
            .Returns(_webhookLogRepoMock.Object);
    }

    private class TestableWebhookLogPrunerService : WebhookLogPrunerService
    {
        public TestableWebhookLogPrunerService(
            IServiceProvider services,
            ILogger<WebhookLogPrunerService> logger)
            : base(services, logger)
        {
        }

        public async Task RunAsync(CancellationToken token)
        {
            try
            {
                await ExecuteAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Expected termination for BackgroundService when cancellation is triggered
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(150)]
    public async Task ExecuteAsync_WhenExpiredLogsExist_PurgesWithSevenDayRetention(int rowsPruned)
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _webhookLogRepoMock
            .Setup(r => r.PurgeExpiredLogsAsync(7))
            .Callback(() => cts.Cancel()) // Cancel immediately after first execution to terminate loop
            .ReturnsAsync(rowsPruned);

        var service = new TestableWebhookLogPrunerService(_rootProviderMock.Object, _loggerMock.Object);

        // Act
        await service.RunAsync(cts.Token);

        // Assert: Verifies that purge was executed with retention period 7
        _webhookLogRepoMock.Verify(r => r.PurgeExpiredLogsAsync(7), Times.Once);
        _scopeMock.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoLogsToPurge_ExecutesWithoutError()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _webhookLogRepoMock
            .Setup(r => r.PurgeExpiredLogsAsync(7))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(0);

        var service = new TestableWebhookLogPrunerService(_rootProviderMock.Object, _loggerMock.Object);

        // Act
        await service.RunAsync(cts.Token);

        // Assert
        _webhookLogRepoMock.Verify(r => r.PurgeExpiredLogsAsync(7), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_HandlesExceptionGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _webhookLogRepoMock
            .Setup(r => r.PurgeExpiredLogsAsync(7))
            .Callback(() => cts.Cancel())
            .ThrowsAsync(new InvalidOperationException("Simulated database timeout"));

        var service = new TestableWebhookLogPrunerService(_rootProviderMock.Object, _loggerMock.Object);

        // Act - should not bubble exception out of ExecuteAsync
        var runTask = service.RunAsync(cts.Token);
        await runTask;

        // Assert
        _webhookLogRepoMock.Verify(r => r.PurgeExpiredLogsAsync(7), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyCancelled_NeverQueriesRepository()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        var service = new TestableWebhookLogPrunerService(_rootProviderMock.Object, _loggerMock.Object);

        // Act
        await service.RunAsync(cts.Token);

        // Assert: loop does not enter
        _webhookLogRepoMock.Verify(r => r.PurgeExpiredLogsAsync(It.IsAny<int>()), Times.Never);
        _scopeFactoryMock.Verify(f => f.CreateScope(), Times.Never);
    }
}
