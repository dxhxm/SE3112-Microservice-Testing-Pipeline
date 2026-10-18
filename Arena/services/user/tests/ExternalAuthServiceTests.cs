using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Entities;
using UserService.Models;
using UserService.Repositories;
using UserService.Services;
using Xunit;

namespace UserService.Tests;

public class ExternalAuthServiceTests
{
    private readonly Mock<IExternalAuthRepository> _externalAuthRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly Mock<ILogger<ExternalAuthService>> _loggerMock;
    private readonly ExternalAuthService _service;

    public ExternalAuthServiceTests()
    {
        _externalAuthRepoMock = new Mock<IExternalAuthRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _loggerMock = new Mock<ILogger<ExternalAuthService>>();

        _service = new ExternalAuthService(
            _externalAuthRepoMock.Object,
            _userRepoMock.Object,
            _jwtTokenGeneratorMock.Object,
            _loggerMock.Object);
    }

    #region Helper Methods

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TwitchTestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    #endregion

    #region ProcessTwitchLoginAsync Tests

    [Fact]
    public async Task ProcessTwitchLoginAsync_MissingTwitchIdClaim_ThrowsInvalidOperationException()
    {
        // Arrange: Principal with email and username, but no NameIdentifier or sub claim
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Email, "player@arena.gg"),
            new Claim(ClaimTypes.Name, "arena_player"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ProcessTwitchLoginAsync(principal));

        Assert.Equal("Twitch user ID claim not found.", ex.Message);

        // Verify no repository interactions took place
        _externalAuthRepoMock.Verify(r => r.GetUserByTwitchIdAsync(It.IsAny<string>()), Times.Never);
        _externalAuthRepoMock.Verify(r => r.SaveLoginCodeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ProcessTwitchLoginAsync_WithSubClaimWhenNameIdentifierMissing_ResolvesTwitchUserId()
    {
        // Arrange: Principal using OpenID "sub" claim instead of ClaimTypes.NameIdentifier
        const string twitchId = "twitch_sub_456";
        var existingUser = new User
        {
            UserId = 15,
            Username = "sub_user",
            Email = "sub@arena.gg",
            Role = "Viewer"
        };

        var principal = CreatePrincipal(
            new Claim("sub", twitchId),
            new Claim("email", "sub@arena.gg"));

        _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
            .ReturnsAsync(existingUser);

        // Act
        var rawCode = await _service.ProcessTwitchLoginAsync(principal);

        // Assert
        Assert.NotNull(rawCode);
        Assert.Equal(64, rawCode.Length);

        _externalAuthRepoMock.Verify(r => r.GetUserByTwitchIdAsync(twitchId), Times.Once);
        _externalAuthRepoMock.Verify(r => r.SaveLoginCodeAsync(
            ComputeSha256(rawCode), existingUser.UserId, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTwitchLoginAsync_ExistingTwitchAccount_SavesLoginCodeAndReturnsRawCode()
    {
        // Arrange: User is already linked via Twitch ID
        const string twitchId = "twitch_linked_101";
        var existingUser = new User
        {
            UserId = 20,
            Username = "veteran_gamer",
            Email = "veteran@arena.gg",
            Role = "Organizer"
        };

        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, twitchId),
            new Claim(ClaimTypes.Email, "veteran@arena.gg"),
            new Claim(ClaimTypes.Name, "veteran_gamer"));

        _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
            .ReturnsAsync(existingUser);

        // Act
        var rawCode = await _service.ProcessTwitchLoginAsync(principal);

        // Assert: Returns a 64-character lowercase hex string (32 bytes)
        Assert.NotNull(rawCode);
        Assert.Equal(64, rawCode.Length);
        Assert.Matches("^[0-9a-f]{64}$", rawCode);

        // Verify: Saves login code with SHA256 hash and never queries email or links/provisions
        _externalAuthRepoMock.Verify(r => r.SaveLoginCodeAsync(
            ComputeSha256(rawCode), existingUser.UserId, It.IsAny<DateTime>()), Times.Once);
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        _externalAuthRepoMock.Verify(r => r.LinkTwitchAccountAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _externalAuthRepoMock.Verify(r => r.CreateUserFromTwitchAsync(It.IsAny<TwitchUserInfo>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessTwitchLoginAsync_UnlinkedTwitchAccount_MissingOrWhitespaceEmail_ThrowsInvalidOperationException(string? invalidEmail)
    {
        // Arrange: Unlinked Twitch account without a valid email address
        const string twitchId = "twitch_no_email_1";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, twitchId)
        };
        if (invalidEmail != null)
        {
            claims.Add(new Claim(ClaimTypes.Email, invalidEmail));
        }

        var principal = CreatePrincipal(claims.ToArray());

        _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ProcessTwitchLoginAsync(principal));

        Assert.Equal("Twitch account has no email address or permission was denied.", ex.Message);

        _externalAuthRepoMock.Verify(r => r.GetUserByTwitchIdAsync(twitchId), Times.Once);
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        _externalAuthRepoMock.Verify(r => r.CreateUserFromTwitchAsync(It.IsAny<TwitchUserInfo>()), Times.Never);
        _externalAuthRepoMock.Verify(r => r.SaveLoginCodeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ProcessTwitchLoginAsync_UnlinkedTwitchAccount_MatchingEmailExists_AutoLinksAndSavesCode()
    {
        // Arrange: Unlinked Twitch account, but email matches an existing local user
        const string twitchId = "twitch_new_link_99";
        const string email = "streamer@arena.gg";
        var existingEmailUser = new User
        {
            UserId = 42,
            Username = "streamer42",
            Email = email,
            Role = "Viewer"
        };

        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, twitchId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, "streamer42"));

        _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(existingEmailUser);

        // Act
        var rawCode = await _service.ProcessTwitchLoginAsync(principal);

        // Assert
        Assert.NotNull(rawCode);
        Assert.Equal(64, rawCode.Length);

        // Verify: Links account and persists code, but never calls CreateUserFromTwitchAsync
        _externalAuthRepoMock.Verify(r => r.LinkTwitchAccountAsync(existingEmailUser.UserId, twitchId), Times.Once);
        _externalAuthRepoMock.Verify(r => r.CreateUserFromTwitchAsync(It.IsAny<TwitchUserInfo>()), Times.Never);
        _externalAuthRepoMock.Verify(r => r.SaveLoginCodeAsync(
            ComputeSha256(rawCode), existingEmailUser.UserId, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTwitchLoginAsync_UnlinkedTwitchAccount_NoMatchingEmail_CreatesNewUserAndSavesCode()
    {
        // Arrange: Brand-new user with Twitch ID and email not found in local records
        const string twitchId = "twitch_fresh_77";
        const string email = "new_challenger@arena.gg";
        const string username = "fresh_challenger";
        const string displayName = "Fresh Challenger";
        const string avatarUrl = "https://static-cdn.jtvnw.net/jtv_user_pictures/avatar.png";

        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, twitchId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, username),
            new Claim("urn:twitch:displayname", displayName),
            new Claim("urn:twitch:profileimageurl", avatarUrl));

        var createdUser = new User
        {
            UserId = 77,
            Username = username,
            Email = email,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            Role = "Viewer"
        };

        _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _externalAuthRepoMock.Setup(r => r.CreateUserFromTwitchAsync(It.IsAny<TwitchUserInfo>()))
            .ReturnsAsync(createdUser);

        // Act
        var rawCode = await _service.ProcessTwitchLoginAsync(principal);

        // Assert
        Assert.NotNull(rawCode);
        Assert.Equal(64, rawCode.Length);

        // Verify: Correct TwitchUserInfo DTO passed to CreateUserFromTwitchAsync
        _externalAuthRepoMock.Verify(r => r.CreateUserFromTwitchAsync(It.Is<TwitchUserInfo>(info =>
            info.TwitchUserId == twitchId &&
            info.Email == email &&
            info.Username == username &&
            info.DisplayName == displayName &&
            info.AvatarUrl == avatarUrl
        )), Times.Once);

        _externalAuthRepoMock.Verify(r => r.LinkTwitchAccountAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _externalAuthRepoMock.Verify(r => r.SaveLoginCodeAsync(
            ComputeSha256(rawCode), createdUser.UserId, It.IsAny<DateTime>()), Times.Once);
    }

    [Theory]
    [InlineData("ClaimTypesName", null, "test@arena.gg", "ClaimTypesName")]
    [InlineData(null, "preferred_nick", "test@arena.gg", "preferred_nick")]
    [InlineData(null, null, "streamer_alpha@arena.gg", "streamer_alpha")]
    [InlineData(null, null, null, "TwitchGamer")]
    public async Task ProcessTwitchLoginAsync_UsernameFallbacks_ResolvesExpectedUsername(
        string? nameClaim,
        string? preferredUsernameClaim,
        string? emailClaim,
        string expectedUsername)
    {
        // Arrange
        const string twitchId = "twitch_fallback_user";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, twitchId)
        };

        if (nameClaim != null) claims.Add(new Claim(ClaimTypes.Name, nameClaim));
        if (preferredUsernameClaim != null) claims.Add(new Claim("preferred_username", preferredUsernameClaim));
        if (emailClaim != null) claims.Add(new Claim(ClaimTypes.Email, emailClaim));

        var principal = CreatePrincipal(claims.ToArray());

        // For tests where email is null, stub an existing user so it doesn't fail the null email check
        if (emailClaim == null)
        {
            _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
                .ReturnsAsync(new User { UserId = 5, Username = expectedUsername, Email = "fallback@arena.gg" });
        }
        else
        {
            _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
                .ReturnsAsync((User?)null);
            _userRepoMock.Setup(r => r.GetByEmailAsync(emailClaim))
                .ReturnsAsync((User?)null);
            _externalAuthRepoMock.Setup(r => r.CreateUserFromTwitchAsync(It.IsAny<TwitchUserInfo>()))
                .ReturnsAsync(new User { UserId = 5, Username = expectedUsername, Email = emailClaim });
        }

        // Act
        var rawCode = await _service.ProcessTwitchLoginAsync(principal);

        // Assert
        Assert.NotNull(rawCode);

        if (emailClaim != null)
        {
            _externalAuthRepoMock.Verify(r => r.CreateUserFromTwitchAsync(
                It.Is<TwitchUserInfo>(info => info.Username == expectedUsername)), Times.Once);
        }
    }

    [Theory]
    [InlineData("https://cdn.twitch.tv/profile.png", null, null, "https://cdn.twitch.tv/profile.png")]
    [InlineData(null, "https://cdn.twitch.tv/picture.png", null, "https://cdn.twitch.tv/picture.png")]
    [InlineData(null, null, "https://cdn.twitch.tv/avatar_url.png", "https://cdn.twitch.tv/avatar_url.png")]
    [InlineData(null, null, null, null)]
    public async Task ProcessTwitchLoginAsync_AvatarUrlFallbacks_ResolvesExpectedAvatar(
        string? profileImageUrlClaim,
        string? pictureClaim,
        string? avatarUrlClaim,
        string? expectedAvatar)
    {
        // Arrange
        const string twitchId = "twitch_avatar_test";
        const string email = "avatar_user@arena.gg";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, twitchId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, "avatar_gamer")
        };

        if (profileImageUrlClaim != null) claims.Add(new Claim("urn:twitch:profileimageurl", profileImageUrlClaim));
        if (pictureClaim != null) claims.Add(new Claim("picture", pictureClaim));
        if (avatarUrlClaim != null) claims.Add(new Claim("avatar_url", avatarUrlClaim));

        var principal = CreatePrincipal(claims.ToArray());

        _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _externalAuthRepoMock.Setup(r => r.CreateUserFromTwitchAsync(It.IsAny<TwitchUserInfo>()))
            .ReturnsAsync(new User { UserId = 6, Username = "avatar_gamer", Email = email, AvatarUrl = expectedAvatar });

        // Act
        var rawCode = await _service.ProcessTwitchLoginAsync(principal);

        // Assert
        Assert.NotNull(rawCode);
        _externalAuthRepoMock.Verify(r => r.CreateUserFromTwitchAsync(
            It.Is<TwitchUserInfo>(info => info.AvatarUrl == expectedAvatar)), Times.Once);
    }

    [Theory]
    [InlineData("DisplayNameUrn", null, null, "DisplayNameUrn")]
    [InlineData(null, "NameOpenId", null, "NameOpenId")]
    [InlineData(null, null, "ClaimTypesName", "ClaimTypesName")]
    [InlineData(null, null, null, "gamer_username")] // Falls back to username
    public async Task ProcessTwitchLoginAsync_DisplayNameFallbacks_ResolvesExpectedDisplayName(
        string? urnDisplayName,
        string? nameClaim,
        string? claimTypesName,
        string expectedDisplayName)
    {
        // Arrange
        const string twitchId = "twitch_display_test";
        const string email = "display@arena.gg";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, twitchId),
            new(ClaimTypes.Email, email),
            new("preferred_username", "gamer_username")
        };

        if (urnDisplayName != null) claims.Add(new Claim("urn:twitch:displayname", urnDisplayName));
        if (nameClaim != null) claims.Add(new Claim("name", nameClaim));
        if (claimTypesName != null) claims.Add(new Claim(ClaimTypes.Name, claimTypesName));

        var principal = CreatePrincipal(claims.ToArray());

        _externalAuthRepoMock.Setup(r => r.GetUserByTwitchIdAsync(twitchId))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _externalAuthRepoMock.Setup(r => r.CreateUserFromTwitchAsync(It.IsAny<TwitchUserInfo>()))
            .ReturnsAsync(new User { UserId = 7, Username = "gamer_username", Email = email, DisplayName = expectedDisplayName });

        // Act
        var rawCode = await _service.ProcessTwitchLoginAsync(principal);

        // Assert
        Assert.NotNull(rawCode);
        _externalAuthRepoMock.Verify(r => r.CreateUserFromTwitchAsync(
            It.Is<TwitchUserInfo>(info => info.DisplayName == expectedDisplayName)), Times.Once);
    }

    #endregion

    #region ExchangeCodeAsync Tests

    [Fact]
    public async Task ExchangeCodeAsync_InvalidOrExpiredCode_ThrowsUnauthorizedAccessException()
    {
        // Arrange: Repository returns null for consumed code hash
        const string invalidRawCode = "invalid_or_expired_code_123456789";
        var expectedHash = ComputeSha256(invalidRawCode);

        _externalAuthRepoMock.Setup(r => r.ConsumeLoginCodeAsync(expectedHash))
            .ReturnsAsync((int?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ExchangeCodeAsync(invalidRawCode));

        Assert.Equal("Login code is invalid or has expired.", ex.Message);

        _externalAuthRepoMock.Verify(r => r.ConsumeLoginCodeAsync(expectedHash), Times.Once);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        _jwtTokenGeneratorMock.Verify(g => g.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeCodeAsync_UserNotFoundAfterCodeConsumption_ThrowsUnauthorizedAccessException()
    {
        // Arrange: Code is consumed successfully and yields a userId, but user does not exist in DB
        const string validRawCode = "valid_raw_code_for_missing_user";
        const int userId = 999;
        var expectedHash = ComputeSha256(validRawCode);

        _externalAuthRepoMock.Setup(r => r.ConsumeLoginCodeAsync(expectedHash))
            .ReturnsAsync(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ExchangeCodeAsync(validRawCode));

        Assert.Equal("User not found.", ex.Message);

        _externalAuthRepoMock.Verify(r => r.ConsumeLoginCodeAsync(expectedHash), Times.Once);
        _userRepoMock.Verify(r => r.GetByIdAsync(userId), Times.Once);
        _jwtTokenGeneratorMock.Verify(g => g.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ValidCodeAndExistingUser_ReturnsLoginResponseWithJwtAndMappedFields()
    {
        // Arrange: Valid code consumed, user found, JWT generated
        const string validRawCode = "valid_hex_exchange_code_abcdef0123456789";
        const int userId = 50;
        const string expectedToken = "jwt_bearer_token_string_example";
        var expectedHash = ComputeSha256(validRawCode);

        var existingUser = new User
        {
            UserId = userId,
            Username = "esports_champion",
            Email = "champion@arena.gg",
            Role = "Organizer"
        };

        _externalAuthRepoMock.Setup(r => r.ConsumeLoginCodeAsync(expectedHash))
            .ReturnsAsync(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(existingUser);
        _jwtTokenGeneratorMock.SetupGet(g => g.ExpiryMinutes)
            .Returns(60);
        _jwtTokenGeneratorMock.Setup(g => g.GenerateToken(existingUser))
            .Returns(expectedToken);

        // Act
        var response = await _service.ExchangeCodeAsync(validRawCode);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(expectedToken, response.Token);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(3600, response.ExpiresIn); // 60 minutes * 60 seconds
        Assert.Equal(userId, response.UserId);
        Assert.Equal("esports_champion", response.Username);
        Assert.Equal("champion@arena.gg", response.Email);
        Assert.Equal("Organizer", response.Role);

        // Verify: Strict invocation counts
        _externalAuthRepoMock.Verify(r => r.ConsumeLoginCodeAsync(expectedHash), Times.Once);
        _userRepoMock.Verify(r => r.GetByIdAsync(userId), Times.Once);
        _jwtTokenGeneratorMock.Verify(g => g.GenerateToken(existingUser), Times.Once);
    }

    #endregion
}
