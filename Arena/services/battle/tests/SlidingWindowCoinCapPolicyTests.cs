using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using BattleEconomyService.Configuration;
using BattleEconomyService.Repositories;
using BattleEconomyService.Services;
using Xunit;

namespace BattleEconomyService.Tests;

public class SlidingWindowCoinCapPolicyTests
{
    private readonly Mock<ICoinTransactionRepository> _txRepoMock;
    private readonly FakeTimeProvider _timeProvider;
    private const int UserId = 42;
    private const int StreamId = 101;

    public SlidingWindowCoinCapPolicyTests()
    {
        _txRepoMock = new Mock<ICoinTransactionRepository>();
        // Fixed test clock: 2026-09-20 12:00:00 UTC
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
    }

    private SlidingWindowCoinCapPolicy CreatePolicy(int capPerWindow = 50, int windowSeconds = 300) =>
        new(
            _txRepoMock.Object,
            Options.Create(new EconomyOptions
            {
                WatchTickIntervalSeconds = 55,
                WatchTickCoinsAwarded = 10,
                CoinCapWindowSeconds = windowSeconds,
                CoinCapPerWindow = capPerWindow
            }),
            _timeProvider
        );

    // =========================================================================
    // Helper: expected window start given the current frozen clock
    // =========================================================================
    private DateTime ExpectedWindowStart(int windowSeconds = 300) =>
        _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(-windowSeconds);

    // =========================================================================
    // Test 1: Window sum below cap → false
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_SumBelowCap_ReturnsFalse()
    {
        // 40 coins earned, cap is 50
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, It.IsAny<DateTime>()))
            .ReturnsAsync(40);

        var policy = CreatePolicy(capPerWindow: 50);
        var result = await policy.IsCapExceededAsync(UserId, StreamId);

        Assert.False(result);
    }

    // =========================================================================
    // Test 2: Window sum exactly equal to cap → true (boundary inclusive)
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_SumExactlyEqualToCap_ReturnsTrue()
    {
        // Exactly 50 coins earned, cap is 50
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, It.IsAny<DateTime>()))
            .ReturnsAsync(50);

        var policy = CreatePolicy(capPerWindow: 50);
        var result = await policy.IsCapExceededAsync(UserId, StreamId);

        Assert.True(result);
    }

    // =========================================================================
    // Test 3: Window sum above cap → true
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_SumAboveCap_ReturnsTrue()
    {
        // 60 coins somehow accumulated (e.g., race with multiple in-flight requests)
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, It.IsAny<DateTime>()))
            .ReturnsAsync(60);

        var policy = CreatePolicy(capPerWindow: 50);
        var result = await policy.IsCapExceededAsync(UserId, StreamId);

        Assert.True(result);
    }

    // =========================================================================
    // Test 4: Window boundary — exactly at 300-second mark uses correct windowStart
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_PassesCorrectWindowStartToRepository()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expectedWindowStart = now.AddSeconds(-300);

        DateTime capturedWindowStart = default;
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, It.IsAny<DateTime>()))
            .Callback<int, int?, DateTime>((_, _, ws) => capturedWindowStart = ws)
            .ReturnsAsync(0);

        var policy = CreatePolicy(windowSeconds: 300);
        await policy.IsCapExceededAsync(UserId, StreamId);

        Assert.Equal(expectedWindowStart, capturedWindowStart);
    }

    // =========================================================================
    // Test 5: Still inside window — cap applies from windowStart forward
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_StillInsideWindow_CapEnforced()
    {
        // Advance clock by 150 seconds (halfway through the 300s window)
        _timeProvider.Advance(TimeSpan.FromSeconds(150));

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expectedWindowStart = now.AddSeconds(-300);

        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, expectedWindowStart))
            .ReturnsAsync(50); // cap reached

        var policy = CreatePolicy(windowSeconds: 300, capPerWindow: 50);
        var result = await policy.IsCapExceededAsync(UserId, StreamId);

        Assert.True(result);
    }

    // =========================================================================
    // Test 6: After window expires — old transactions fall outside new windowStart
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_AfterWindowExpires_ReturnsFalse()
    {
        // Advance clock by 301 seconds — the previous window is now outside the range
        _timeProvider.Advance(TimeSpan.FromSeconds(301));

        // Repository returns 0 because the new windowStart excludes old transactions
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var policy = CreatePolicy(capPerWindow: 50);
        var result = await policy.IsCapExceededAsync(UserId, StreamId);

        Assert.False(result);
        // Verify the windowStart sent to the repository is after the old transactions
        _txRepoMock.Verify(r => r.GetWindowCoinSumAsync(
            UserId, StreamId,
            It.Is<DateTime>(ws => ws > new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc))),
            Times.Once);
    }

    // =========================================================================
    // Test 7 (AC3): Stream A cap does not affect Stream B
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_StreamACapDoesNotAffectStreamB()
    {
        const int streamA = 101;
        const int streamB = 202;

        // Stream A is at cap
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, streamA, It.IsAny<DateTime>()))
            .ReturnsAsync(50);
        // Stream B has earned nothing
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, streamB, It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var policy = CreatePolicy(capPerWindow: 50);

        var capOnA = await policy.IsCapExceededAsync(UserId, streamA);
        var capOnB = await policy.IsCapExceededAsync(UserId, streamB);

        Assert.True(capOnA, "Stream A should be capped");
        Assert.False(capOnB, "Stream B should NOT be capped");
    }

    // =========================================================================
    // Test 8: Null streamId — always returns false (no global per-user cap)
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_NullStreamId_ReturnsFalseWithoutQueryingRepository()
    {
        var policy = CreatePolicy();
        var result = await policy.IsCapExceededAsync(UserId, streamId: null);

        Assert.False(result);
        // Repository must NOT be called when streamId is null
        _txRepoMock.Verify(r => r.GetWindowCoinSumAsync(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTime>()), Times.Never);
    }

    // =========================================================================
    // Test 9: Zero sum (no transactions yet) → false
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_NoTransactionsInWindow_ReturnsFalse()
    {
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var policy = CreatePolicy();
        var result = await policy.IsCapExceededAsync(UserId, StreamId);

        Assert.False(result);
    }

    // =========================================================================
    // Test 10: One coin below cap → false (boundary below)
    // =========================================================================
    [Fact]
    public async Task IsCapExceededAsync_SumOneBeforeCap_ReturnsFalse()
    {
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, It.IsAny<DateTime>()))
            .ReturnsAsync(49); // cap is 50

        var policy = CreatePolicy(capPerWindow: 50);
        var result = await policy.IsCapExceededAsync(UserId, StreamId);

        Assert.False(result);
    }

    // =========================================================================
    // Parameterized Boundary-Value Tests ([Theory] with [InlineData])
    // =========================================================================

    /// <summary>
    /// Verifies the three-point boundary condition around configured coin caps:
    /// exactly one coin below the cap (sum = cap - 1) -> false
    /// exactly at the cap (sum = cap) -> true (boundary inclusive)
    /// exactly one coin above the cap (sum = cap + 1) -> true
    /// tested across different cap configurations (e.g. 10, 50, 100, 500).
    /// </summary>
    [Theory]
    // Baseline Cap = 50
    [InlineData(50, 0, false)]
    [InlineData(50, 49, false)]
    [InlineData(50, 50, true)]
    [InlineData(50, 51, true)]
    [InlineData(50, 100, true)]
    // Low Cap = 10
    [InlineData(10, 0, false)]
    [InlineData(10, 9, false)]
    [InlineData(10, 10, true)]
    [InlineData(10, 11, true)]
    // High Cap = 100
    [InlineData(100, 99, false)]
    [InlineData(100, 100, true)]
    [InlineData(100, 101, true)]
    // Extreme Cap = 500
    [InlineData(500, 499, false)]
    [InlineData(500, 500, true)]
    [InlineData(500, 501, true)]
    public async Task IsCapExceededAsync_BoundaryValuesAroundConfiguredCap_ReturnsExpectedCapStatus(
        int configuredCap,
        int windowSum,
        bool expectedExceeded)
    {
        // Arrange
        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(UserId, StreamId, It.IsAny<DateTime>()))
            .ReturnsAsync(windowSum);

        var policy = CreatePolicy(capPerWindow: configuredCap);

        // Act
        var isExceeded = await policy.IsCapExceededAsync(UserId, StreamId);

        // Assert
        Assert.Equal(expectedExceeded, isExceeded);

        // Verify repository was queried exactly once with user and stream ID
        _txRepoMock.Verify(r => r.GetWindowCoinSumAsync(
            UserId, StreamId, It.IsAny<DateTime>()), Times.Once);
    }

    // =========================================================================
    // Parameterized Time-Window Tests ([Theory] with [MemberData])
    // =========================================================================

    public static TheoryData<int, int, TimeSpan, int, int, int, bool> TimeWindowTestData => new()
    {
        // Parameters: userId, streamId, clockAdvance, windowSeconds, capPerWindow, windowSum, expectedExceeded

        // Scenario 1: Clock not advanced, 60s window, sum = 40, cap = 50 -> not exceeded
        { 42, 101, TimeSpan.Zero, 60, 50, 40, false },

        // Scenario 2: Clock not advanced, 60s window, sum = 50, cap = 50 -> exceeded
        { 42, 101, TimeSpan.Zero, 60, 50, 50, true },

        // Scenario 3: Clock advanced 120s into a 300s window, sum = 49, cap = 50 -> not exceeded
        { 77, 202, TimeSpan.FromSeconds(120), 300, 50, 49, false },

        // Scenario 4: Clock advanced 120s into a 300s window, sum = 50, cap = 50 -> exceeded
        { 77, 202, TimeSpan.FromSeconds(120), 300, 50, 50, true },

        // Scenario 5: Clock advanced 500s, 600s window, sum = 199, cap = 200 -> not exceeded
        { 88, 303, TimeSpan.FromSeconds(500), 600, 200, 199, false },

        // Scenario 6: Clock advanced 500s, 600s window, sum = 200, cap = 200 -> exceeded
        { 88, 303, TimeSpan.FromSeconds(500), 600, 200, 200, true },

        // Scenario 7: Extended window (900s), clock advanced 15m, sum = 301, cap = 300 -> exceeded
        { 99, 404, TimeSpan.FromMinutes(15), 900, 300, 301, true }
    };

    /// <summary>
    /// Verifies that across varying sliding-window configurations and virtual clock progressions,
    /// the calculated windowStart passed to the repository matches precisely:
    /// windowStart = current_utc_time - window_seconds.
    /// Also verifies strict parameter matching and invocation counts via Moq.
    /// </summary>
    [Theory]
    [MemberData(nameof(TimeWindowTestData))]
    public async Task IsCapExceededAsync_VaryingTimeWindowsAndClockProgression_QueriesExactWindowStart(
        int userId,
        int streamId,
        TimeSpan clockAdvance,
        int windowSeconds,
        int capPerWindow,
        int windowSum,
        bool expectedExceeded)
    {
        // Arrange
        if (clockAdvance > TimeSpan.Zero)
        {
            _timeProvider.Advance(clockAdvance);
        }

        var currentUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var expectedWindowStart = currentUtc.AddSeconds(-windowSeconds);

        _txRepoMock.Setup(r => r.GetWindowCoinSumAsync(userId, streamId, expectedWindowStart))
            .ReturnsAsync(windowSum);

        var policy = CreatePolicy(capPerWindow: capPerWindow, windowSeconds: windowSeconds);

        // Act
        var result = await policy.IsCapExceededAsync(userId, streamId);

        // Assert
        Assert.Equal(expectedExceeded, result);

        // Verify repository was queried with the exact user, stream, and computed windowStart
        _txRepoMock.Verify(r => r.GetWindowCoinSumAsync(userId, streamId, expectedWindowStart), Times.Once);
    }

    // =========================================================================
    // Null streamId Parameterized Isolation Tests ([Theory] with [InlineData])
    // =========================================================================

    /// <summary>
    /// Verifies that regardless of user ID or configured cap, when streamId is null,
    /// the cap check is skipped immediately, returning false without touching the repository.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(100)]
    [InlineData(9999)]
    public async Task IsCapExceededAsync_NullStreamIdAcrossVariousUsers_NeverQueriesRepository(int userId)
    {
        // Arrange
        var policy = CreatePolicy(capPerWindow: 20, windowSeconds: 120);

        // Act
        var result = await policy.IsCapExceededAsync(userId, streamId: null);

        // Assert
        Assert.False(result);

        // Strict verification: repository must NEVER be queried when stream context is absent
        _txRepoMock.Verify(r => r.GetWindowCoinSumAsync(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTime>()), Times.Never);
    }
}
