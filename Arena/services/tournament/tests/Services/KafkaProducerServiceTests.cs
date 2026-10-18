using System.Reflection;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using TournamentService.Services;
using Arena.Shared.EventContracts;
using Xunit;

namespace TournamentService.Tests.Services;

public class KafkaProducerServiceTests
{
    private readonly Mock<IProducer<string, string>> _producerMock;
    private readonly Mock<ILogger<KafkaProducerService>> _loggerMock;
    private const string ExpectedTopic = "team.events";

    public KafkaProducerServiceTests()
    {
        _producerMock = new Mock<IProducer<string, string>>();
        _loggerMock = new Mock<ILogger<KafkaProducerService>>();

        // Default successful delivery result
        _producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string topic, Message<string, string> msg, CancellationToken ct) =>
                new DeliveryResult<string, string>
                {
                    Topic = topic,
                    Partition = new Partition(0),
                    Offset = new Offset(42),
                    Status = PersistenceStatus.Persisted,
                    Message = msg
                });
    }

    /// <summary>
    /// Instantiates <see cref="KafkaProducerService"/> using the reflection pattern established
    /// in the testing pipeline to inject a mocked <see cref="IProducer{string, string}"/>
    /// without attempting network connections to a live Kafka broker.
    /// </summary>
    private KafkaProducerService CreateService()
    {
        var service = (KafkaProducerService)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(KafkaProducerService));

        var type = typeof(KafkaProducerService);
        type.GetField("_producer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, _producerMock.Object);
        type.GetField("_logger", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, _loggerMock.Object);

        return service;
    }

    [Theory]
    [InlineData(1, "Team Crimson", "Created")]
    [InlineData(2, "Team Cobalt", "Updated")]
    [InlineData(99, "Team Shadow", "Deleted")]
    public async Task PublishTeamChangedAsync_ValidEvent_PublishesToTeamEventsTopicWithKeyAndCamelCaseJson(
        int teamId,
        string teamName,
        string changeType)
    {
        // Arrange
        var service = CreateService();
        var evt = new TeamChangedEvent
        {
            TeamId = teamId,
            TeamName = teamName,
            ChangeType = changeType
        };

        // Act
        await service.PublishTeamChangedAsync(evt);

        // Assert: Verifies that message was dispatched to "team.events" with key equal to teamId
        _producerMock.Verify(p => p.ProduceAsync(
            ExpectedTopic,
            It.Is<Message<string, string>>(m =>
                m.Key == teamId.ToString() &&
                m.Value.Contains($"\"teamId\":{teamId}") &&
                m.Value.Contains($"\"teamName\":\"{teamName}\"") &&
                m.Value.Contains($"\"changeType\":\"{changeType}\"")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishTeamChangedAsync_WhenProduceThrowsProduceException_LogsErrorAndDoesNotRethrow()
    {
        // Arrange: Simulate broker connection failure / timeout
        _producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, string>(
                new Error(ErrorCode.Local_Transport, "Broker transport error"),
                new DeliveryResult<string, string> { Topic = ExpectedTopic }));

        var service = CreateService();
        var evt = new TeamChangedEvent { TeamId = 5, TeamName = "Alpha", ChangeType = "Created" };

        // Act & Assert: Must complete without throwing (fire-and-forget resiliency)
        await service.PublishTeamChangedAsync(evt);

        _producerMock.Verify(p => p.ProduceAsync(
            ExpectedTopic,
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("arena.custom.topic", "key-101")]
    [InlineData("arena.events.v2", "key-202")]
    public async Task PublishAsync_GenericPayload_PublishesSerializedMessageToSpecifiedTopic(
        string customTopic,
        string customKey)
    {
        // Arrange
        var service = CreateService();
        var payload = new { TournamentId = 10, Action = "Started", Timestamp = "2026-10-18T12:00:00Z" };

        // Act
        await service.PublishAsync(customTopic, customKey, payload);

        // Assert
        _producerMock.Verify(p => p.ProduceAsync(
            customTopic,
            It.Is<Message<string, string>>(m =>
                m.Key == customKey &&
                m.Value.Contains("\"tournamentId\":10") &&
                m.Value.Contains("\"action\":\"Started\"")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenProduceThrowsProduceException_LogsErrorAndDoesNotRethrow()
    {
        // Arrange
        _producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, string>(
                new Error(ErrorCode.BrokerNotAvailable, "Broker unavailable"),
                new DeliveryResult<string, string>()));

        var service = CreateService();

        // Act & Assert: Completes gracefully
        await service.PublishAsync("arena.fail.topic", "fail-key", new { Status = "failed" });

        _producerMock.Verify(p => p.ProduceAsync(
            "arena.fail.topic",
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispose_FlushesAndDisposesUnderlyingProducer()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.Dispose();

        // Assert
        _producerMock.Verify(p => p.Flush(TimeSpan.FromSeconds(5)), Times.Once);
        _producerMock.Verify(p => p.Dispose(), Times.Once);
    }
}
