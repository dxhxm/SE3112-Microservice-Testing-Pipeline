using Microsoft.Extensions.Logging;
using Moq;
using AnalyticsService.DTOs;
using AnalyticsService.Entities;
using AnalyticsService.Repositories;
using AnalyticsService.Services;
using Xunit;

namespace AnalyticsService.Tests;

public class BattleStatsServiceTests
{
    private readonly Mock<IAnalyticsRepository> _repositoryMock;
    private readonly Mock<ILogger<BattleStatsService>> _loggerMock;
    private readonly BattleStatsService _service;

    public BattleStatsServiceTests()
    {
        _repositoryMock = new Mock<IAnalyticsRepository>();
        _loggerMock = new Mock<ILogger<BattleStatsService>>();
        _service = new BattleStatsService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAllBattleSummariesAsync_ReturnsMappedSummaries()
    {
        // Arrange
        var summaries = new List<StreamTeamBattleSummary>
        {
            new()
            {
                StreamId = 101,
                TeamId = 1,
                TeamName = "Team Crimson",
                TotalAttacks = 150,
                TotalDamageDealt = 4500L,
                TotalCoinsSpent = 12000L,
                RoundsWon = 3,
                RoundsLost = 1,
                LastAttackAt = DateTime.UtcNow
            },
            new()
            {
                StreamId = 101,
                TeamId = 2,
                TeamName = "Team Azure",
                TotalAttacks = 120,
                TotalDamageDealt = 3800L,
                TotalCoinsSpent = 9500L,
                RoundsWon = 1,
                RoundsLost = 3,
                LastAttackAt = DateTime.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetAllBattleSummariesAsync())
            .ReturnsAsync(summaries);

        // Act
        var result = await _service.GetAllBattleSummariesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Team Crimson", result[0].TeamName);
        Assert.Equal(150, result[0].TotalAttacks);
        Assert.Equal(4500L, result[0].TotalDamageDealt);
        Assert.Equal("Team Azure", result[1].TeamName);
        _repositoryMock.Verify(r => r.GetAllBattleSummariesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllBattleSummariesAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetAllBattleSummariesAsync())
            .ReturnsAsync(new List<StreamTeamBattleSummary>());

        // Act
        var result = await _service.GetAllBattleSummariesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        _repositoryMock.Verify(r => r.GetAllBattleSummariesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetBattleSummariesByStreamIdAsync_ReturnsMappedStreamSummaries()
    {
        // Arrange
        var streamId = 202;
        var summaries = new List<StreamTeamBattleSummary>
        {
            new()
            {
                StreamId = streamId,
                TeamId = 5,
                TeamName = "Team Phoenix",
                TotalAttacks = 80,
                TotalDamageDealt = 2400L,
                TotalCoinsSpent = 6000L,
                RoundsWon = 2,
                RoundsLost = 0,
                LastAttackAt = DateTime.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetBattleSummariesByStreamIdAsync(streamId))
            .ReturnsAsync(summaries);

        // Act
        var result = await _service.GetBattleSummariesByStreamIdAsync(streamId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(streamId, result[0].StreamId);
        Assert.Equal(5, result[0].TeamId);
        Assert.Equal("Team Phoenix", result[0].TeamName);
        _repositoryMock.Verify(r => r.GetBattleSummariesByStreamIdAsync(streamId), Times.Once);
    }

    [Fact]
    public async Task GetRoundOutcomesByStreamIdAsync_ReturnsMappedRoundOutcomes()
    {
        // Arrange
        var streamId = 303;
        var completedAt = DateTime.UtcNow;
        var outcomes = new List<StreamRoundOutcome>
        {
            new()
            {
                StreamId = streamId,
                RoundNumber = 1,
                WinningTeamId = 1,
                WinningTeamName = "Team Crimson",
                TeamAId = 1,
                TeamBId = 2,
                TeamAAttacks = 40,
                TeamBAttacks = 30,
                TeamADamage = 1200,
                TeamBDamage = 900,
                CompletedAt = completedAt
            }
        };

        _repositoryMock
            .Setup(r => r.GetRoundOutcomesByStreamIdAsync(streamId))
            .ReturnsAsync(outcomes);

        // Act
        var result = await _service.GetRoundOutcomesByStreamIdAsync(streamId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(streamId, result[0].StreamId);
        Assert.Equal(1, result[0].RoundNumber);
        Assert.Equal(1, result[0].WinningTeamId);
        Assert.Equal("Team Crimson", result[0].WinningTeamName);
        Assert.Equal(completedAt, result[0].CompletedAt);
        _repositoryMock.Verify(r => r.GetRoundOutcomesByStreamIdAsync(streamId), Times.Once);
    }

    [Fact]
    public async Task GetRoundOutcomesByStreamIdAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        var streamId = 404;
        _repositoryMock
            .Setup(r => r.GetRoundOutcomesByStreamIdAsync(streamId))
            .ReturnsAsync(new List<StreamRoundOutcome>());

        // Act
        var result = await _service.GetRoundOutcomesByStreamIdAsync(streamId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        _repositoryMock.Verify(r => r.GetRoundOutcomesByStreamIdAsync(streamId), Times.Once);
    }

    [Fact]
    public async Task GetStreamBattleDashboardAsync_CombinesTeamsAndRoundsCorrectly()
    {
        // Arrange
        var streamId = 505;
        var summaries = new List<StreamTeamBattleSummary>
        {
            new()
            {
                StreamId = streamId,
                TeamId = 1,
                TeamName = "Team Crimson",
                TotalAttacks = 90,
                TotalDamageDealt = 2700L,
                TotalCoinsSpent = 7000L,
                RoundsWon = 2,
                RoundsLost = 1
            }
        };

        var outcomes = new List<StreamRoundOutcome>
        {
            new()
            {
                StreamId = streamId,
                RoundNumber = 1,
                WinningTeamId = 1,
                WinningTeamName = "Team Crimson",
                TeamAId = 1,
                TeamBId = 2,
                TeamAAttacks = 30,
                TeamBAttacks = 25,
                TeamADamage = 900,
                TeamBDamage = 750,
                CompletedAt = DateTime.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetBattleSummariesByStreamIdAsync(streamId))
            .ReturnsAsync(summaries);
        _repositoryMock
            .Setup(r => r.GetRoundOutcomesByStreamIdAsync(streamId))
            .ReturnsAsync(outcomes);

        // Act
        var result = await _service.GetStreamBattleDashboardAsync(streamId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(streamId, result.StreamId);
        Assert.Single(result.Teams);
        Assert.Single(result.Rounds);
        Assert.Equal("Team Crimson", result.Teams[0].TeamName);
        Assert.Equal(1, result.Rounds[0].RoundNumber);
        _repositoryMock.Verify(r => r.GetBattleSummariesByStreamIdAsync(streamId), Times.Once);
        _repositoryMock.Verify(r => r.GetRoundOutcomesByStreamIdAsync(streamId), Times.Once);
    }

    [Fact]
    public async Task GetStreamBattleDashboardAsync_WhenEmpty_ReturnsEmptyTeamsAndRounds()
    {
        // Arrange
        var streamId = 606;
        _repositoryMock
            .Setup(r => r.GetBattleSummariesByStreamIdAsync(streamId))
            .ReturnsAsync(new List<StreamTeamBattleSummary>());
        _repositoryMock
            .Setup(r => r.GetRoundOutcomesByStreamIdAsync(streamId))
            .ReturnsAsync(new List<StreamRoundOutcome>());

        // Act
        var result = await _service.GetStreamBattleDashboardAsync(streamId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(streamId, result.StreamId);
        Assert.Empty(result.Teams);
        Assert.Empty(result.Rounds);
    }

    [Fact]
    public async Task Service_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Read model access failed");
        _repositoryMock
            .Setup(r => r.GetAllBattleSummariesAsync())
            .ThrowsAsync(expectedException);

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetAllBattleSummariesAsync());

        Assert.Same(expectedException, actualException);
    }

    #region Parameterized Mapping and Aggregation Tests ([Theory])

    [Theory]
    [InlineData(10, 1, "Team Crimson", 50, 1500L, 5000L, 2, 0)]
    [InlineData(20, 2, "Team Cobalt", 75, 2250L, 7500L, 1, 2)]
    [InlineData(99, 5, "Team Omega", 0, 0L, 0L, 0, 0)]
    public async Task GetBattleSummariesByStreamIdAsync_ParameterizedEntities_MapsAllPropertiesAccurately(
        int streamId,
        int teamId,
        string teamName,
        int attacks,
        long damage,
        long coins,
        int won,
        int lost)
    {
        // Arrange
        var summaries = new List<StreamTeamBattleSummary>
        {
            new()
            {
                StreamId = streamId,
                TeamId = teamId,
                TeamName = teamName,
                TotalAttacks = attacks,
                TotalDamageDealt = damage,
                TotalCoinsSpent = coins,
                RoundsWon = won,
                RoundsLost = lost,
                LastAttackAt = DateTime.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetBattleSummariesByStreamIdAsync(streamId))
            .ReturnsAsync(summaries);

        // Act
        var result = await _service.GetBattleSummariesByStreamIdAsync(streamId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var item = result[0];
        Assert.Equal(streamId, item.StreamId);
        Assert.Equal(teamId, item.TeamId);
        Assert.Equal(teamName, item.TeamName);
        Assert.Equal(attacks, item.TotalAttacks);
        Assert.Equal(damage, item.TotalDamageDealt);
        Assert.Equal(coins, item.TotalCoinsSpent);
        Assert.Equal(won, item.RoundsWon);
        Assert.Equal(lost, item.RoundsLost);

        _repositoryMock.Verify(r => r.GetBattleSummariesByStreamIdAsync(streamId), Times.Once);
    }

    [Theory]
    [InlineData(101, 1, 1, "Team Crimson", 20, 15, 600, 450)]
    [InlineData(202, 2, 2, "Team Azure", 40, 38, 1200, 1150)]
    public async Task GetRoundOutcomesByStreamIdAsync_ParameterizedOutcomes_MapsCorrectly(
        int streamId,
        int roundNumber,
        int winningTeamId,
        string winningTeamName,
        int teamAAttacks,
        int teamBAttacks,
        int teamADamage,
        int teamBDamage)
    {
        // Arrange
        var outcomes = new List<StreamRoundOutcome>
        {
            new()
            {
                StreamId = streamId,
                RoundNumber = roundNumber,
                WinningTeamId = winningTeamId,
                WinningTeamName = winningTeamName,
                TeamAId = 1,
                TeamBId = 2,
                TeamAAttacks = teamAAttacks,
                TeamBAttacks = teamBAttacks,
                TeamADamage = teamADamage,
                TeamBDamage = teamBDamage,
                CompletedAt = DateTime.UtcNow
            }
        };

        _repositoryMock
            .Setup(r => r.GetRoundOutcomesByStreamIdAsync(streamId))
            .ReturnsAsync(outcomes);

        // Act
        var result = await _service.GetRoundOutcomesByStreamIdAsync(streamId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var outcome = result[0];
        Assert.Equal(streamId, outcome.StreamId);
        Assert.Equal(roundNumber, outcome.RoundNumber);
        Assert.Equal(winningTeamId, outcome.WinningTeamId);
        Assert.Equal(winningTeamName, outcome.WinningTeamName);
        Assert.Equal(teamAAttacks, outcome.TeamAAttacks);
        Assert.Equal(teamBAttacks, outcome.TeamBAttacks);
        Assert.Equal(teamADamage, outcome.TeamADamage);
        Assert.Equal(teamBDamage, outcome.TeamBDamage);

        _repositoryMock.Verify(r => r.GetRoundOutcomesByStreamIdAsync(streamId), Times.Once);
    }

    #endregion
}
