/*
    unit tests for StreamStatusService verifying state machine transitions,
    cascading updates to matches, and out-of-order execution guards
*/

using Microsoft.Extensions.Logging;
using Moq;
using StreamService.DTOs;
using StreamService.Models;
using StreamService.Repositories;
using StreamService.Services;
using Xunit;

namespace StreamService.Tests.Services;

public class StreamStatusServiceTests
{
    private readonly Mock<IStreamRepository> _streamRepoMock;
    private readonly Mock<IMatchRepository> _matchRepoMock;
    private readonly Mock<ILogger<StreamStatusService>> _loggerMock;
    private readonly StreamStatusService _service;

    public StreamStatusServiceTests()
    {
        _streamRepoMock = new Mock<IStreamRepository>();
        _matchRepoMock = new Mock<IMatchRepository>();
        _loggerMock = new Mock<ILogger<StreamStatusService>>();

        _service = new StreamStatusService(
            _streamRepoMock.Object,
            _matchRepoMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task ProcessStreamStatusUpdateAsync_WhenOnline_TransitionsScheduledToLive()
    {
        // Arrange
        const string channelName = "esl_csgo";
        const int streamId = 100;
        const int matchId = 5;

        var streamResponse = new StreamResponse(streamId, 10, 1, matchId, channelName, "Twitch", "Grand Finals", "arena.gg", StreamStatus.Scheduled, 0, null, null, DateTime.UtcNow);

        _streamRepoMock.Setup(s => s.GetStreamByChannelNameAsync(channelName))
            .ReturnsAsync(streamResponse);
        
        _streamRepoMock.Setup(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Live, StreamStatus.Scheduled))
            .ReturnsAsync(true);
            
        _matchRepoMock.Setup(m => m.UpdateMatchStatusAsync(matchId, StreamStatus.Live, StreamStatus.Scheduled))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProcessStreamStatusUpdateAsync("stream.online", channelName);

        // Assert
        Assert.Equal(streamId, result);
        _streamRepoMock.Verify(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Live, StreamStatus.Scheduled), Times.Once);
        _matchRepoMock.Verify(m => m.UpdateMatchStatusAsync(matchId, StreamStatus.Live, StreamStatus.Scheduled), Times.Once);
    }

    [Fact]
    public async Task ProcessStreamStatusUpdateAsync_WhenOffline_TransitionsLiveToEnded()
    {
        // Arrange
        const string channelName = "esl_csgo";
        const int streamId = 100;
        const int matchId = 5;

        var streamResponse = new StreamResponse(streamId, 10, 1, matchId, channelName, "Twitch", "Grand Finals", "arena.gg", StreamStatus.Live, 0, null, null, DateTime.UtcNow);

        _streamRepoMock.Setup(s => s.GetStreamByChannelNameAsync(channelName))
            .ReturnsAsync(streamResponse);
        
        _streamRepoMock.Setup(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Ended, StreamStatus.Live))
            .ReturnsAsync(true);
            
        _matchRepoMock.Setup(m => m.UpdateMatchStatusAsync(matchId, StreamStatus.Ended, StreamStatus.Live))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProcessStreamStatusUpdateAsync("stream.offline", channelName);

        // Assert
        Assert.Equal(streamId, result);
        _streamRepoMock.Verify(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Ended, StreamStatus.Live), Times.Once);
        _matchRepoMock.Verify(m => m.UpdateMatchStatusAsync(matchId, StreamStatus.Ended, StreamStatus.Live), Times.Once);
    }

    [Fact]
    public async Task ProcessStreamStatusUpdateAsync_WhenUnknownSubscriptionType_ReturnsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.ProcessStreamStatusUpdateAsync("stream.unknown", "esl_csgo"));
    }

    [Fact]
    public async Task ProcessStreamStatusUpdateAsync_WhenStreamNotFound_ReturnsNull()
    {
        // Arrange
        _streamRepoMock.Setup(s => s.GetStreamByChannelNameAsync("unknown_channel"))
            .ReturnsAsync((StreamResponse?)null);

        // Act
        var result = await _service.ProcessStreamStatusUpdateAsync("stream.online", "unknown_channel");

        // Assert
        Assert.Null(result);
        _streamRepoMock.Verify(s => s.UpdateStreamStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessStreamStatusUpdateAsync_WhenOutOfOrderTransition_ReturnsNull()
    {
        // Arrange
        const string channelName = "esl_csgo";
        const int streamId = 100;

        // Simulate an offline event arriving while the stream is still 'Scheduled' (Live transition was missed)
        var streamResponse = new StreamResponse(streamId, 10, 1, 5, channelName, "Twitch", "Grand Finals", "arena.gg", StreamStatus.Scheduled, 0, null, null, DateTime.UtcNow);

        _streamRepoMock.Setup(s => s.GetStreamByChannelNameAsync(channelName))
            .ReturnsAsync(streamResponse);
        
        // The conditional update will fail (returns false) because expected status is 'Live' but current is 'Scheduled'
        _streamRepoMock.Setup(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Ended, StreamStatus.Live))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ProcessStreamStatusUpdateAsync("stream.offline", channelName);

        // Assert
        Assert.Null(result);
        _matchRepoMock.Verify(m => m.UpdateMatchStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("stream.unknown")]
    [InlineData("channel.update")]
    [InlineData("stream.live")]
    [InlineData("")]
    [InlineData("STREAM.ONLINE")]
    public async Task ProcessStreamStatusUpdateAsync_UnsupportedSubscriptionTypes_ThrowsArgumentException(string unsupportedType)
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ProcessStreamStatusUpdateAsync(unsupportedType, "esl_csgo"));

        Assert.Contains("Unsupported subscription type", ex.Message);
        _streamRepoMock.Verify(s => s.GetStreamByChannelNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessStreamStatusUpdateAsync_StreamWithoutMatchId_TransitionsStreamWithoutMatchUpdate()
    {
        // Arrange: Stream exists without an assigned MatchId
        const string channelName = "solo_streamer";
        const int streamId = 200;

        var streamResponse = new StreamResponse(streamId, 10, 1, null, channelName, "Twitch", "Solo Run", "arena.gg", StreamStatus.Scheduled, 0, null, null, DateTime.UtcNow);

        _streamRepoMock.Setup(s => s.GetStreamByChannelNameAsync(channelName))
            .ReturnsAsync(streamResponse);
        _streamRepoMock.Setup(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Live, StreamStatus.Scheduled))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProcessStreamStatusUpdateAsync("stream.online", channelName);

        // Assert
        Assert.Equal(streamId, result);
        _streamRepoMock.Verify(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Live, StreamStatus.Scheduled), Times.Once);
        _matchRepoMock.Verify(m => m.UpdateMatchStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessStreamStatusUpdateAsync_MatchUpdateFails_StillReturnsStreamId()
    {
        // Arrange: Stream update succeeds but cascading match update fails (e.g. concurrent race)
        const string channelName = "match_streamer";
        const int streamId = 300;
        const int matchId = 42;

        var streamResponse = new StreamResponse(streamId, 10, 1, matchId, channelName, "Twitch", "Finals", "arena.gg", StreamStatus.Scheduled, 0, null, null, DateTime.UtcNow);

        _streamRepoMock.Setup(s => s.GetStreamByChannelNameAsync(channelName))
            .ReturnsAsync(streamResponse);
        _streamRepoMock.Setup(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Live, StreamStatus.Scheduled))
            .ReturnsAsync(true);
        _matchRepoMock.Setup(m => m.UpdateMatchStatusAsync(matchId, StreamStatus.Live, StreamStatus.Scheduled))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ProcessStreamStatusUpdateAsync("stream.online", channelName);

        // Assert
        Assert.Equal(streamId, result);
        _streamRepoMock.Verify(s => s.UpdateStreamStatusAsync(streamId, StreamStatus.Live, StreamStatus.Scheduled), Times.Once);
        _matchRepoMock.Verify(m => m.UpdateMatchStatusAsync(matchId, StreamStatus.Live, StreamStatus.Scheduled), Times.Once);
    }
}
