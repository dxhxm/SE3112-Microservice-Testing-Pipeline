using ChatService.Services;
using Xunit;

namespace ChatService.Tests;

public class ProfanityFilterTests
{
    private readonly ProfanityFilter _filter = new();

    #region Existing Preserved Tests

    [Fact]
    public void Scrub_SingleBadWord_ReplacedWithAsterisks()
    {
        var result = _filter.Scrub("you are a damn fool");
        Assert.Equal("you are a *** fool", result);
    }

    [Fact]
    public void Scrub_MultipleBadWords_AllReplaced()
    {
        var result = _filter.Scrub("what the hell is this shit");
        Assert.Equal("what the *** is this ***", result);
    }

    [Fact]
    public void Scrub_CleanMessage_Unchanged()
    {
        var result = _filter.Scrub("good game well played");
        Assert.Equal("good game well played", result);
    }

    [Fact]
    public void Scrub_PartialMatch_NotReplaced()
    {
        // "class" contains "ass" but word boundary prevents false positive
        var result = _filter.Scrub("this is a class act");
        Assert.Equal("this is a class act", result);
    }

    [Fact]
    public void Scrub_CaseInsensitive_Works()
    {
        var result = _filter.Scrub("DAMN that was HELL of a game");
        Assert.Equal("*** that was *** of a game", result);
    }

    [Fact]
    public void ContainsProfanity_WithBadWord_ReturnsTrue()
    {
        Assert.True(_filter.ContainsProfanity("this is damn bad"));
    }

    [Fact]
    public void ContainsProfanity_CleanMessage_ReturnsFalse()
    {
        Assert.False(_filter.ContainsProfanity("good game well played"));
    }

    [Fact]
    public void Scrub_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", _filter.Scrub(""));
    }

    [Fact]
    public void ContainsProfanity_NullString_ReturnsFalse()
    {
        Assert.False(_filter.ContainsProfanity(null!));
    }

    #endregion

    #region Parameterized Word Blacklist Verification ([Theory] with [MemberData])

    public static TheoryData<string> AllBlacklistedWords => new()
    {
        "damn", "hell", "crap", "shit", "fuck", "ass",
        "bitch", "bastard", "dick", "piss", "slut",
        "whore", "idiot", "stupid", "dumb", "moron",
        "retard", "nigger", "faggot", "cock", "pussy",
        "wanker", "twat", "bollocks"
    };

    [Theory]
    [MemberData(nameof(AllBlacklistedWords))]
    public void ContainsProfanity_EveryBlacklistedWordIndividually_ReturnsTrue(string word)
    {
        Assert.True(_filter.ContainsProfanity(word), $"Expected '{word}' to be detected as profanity.");
    }

    [Theory]
    [MemberData(nameof(AllBlacklistedWords))]
    public void Scrub_EveryBlacklistedWordInSentence_ReplacesWithAsterisks(string word)
    {
        var input = $"player said {word} in chat";
        var expected = "player said *** in chat";

        var actual = _filter.Scrub(input);

        Assert.Equal(expected, actual);
    }

    #endregion

    #region Parameterized Substring False-Positive Safety ([Theory] with [InlineData])

    /// <summary>
    /// Verifies word-boundary enforcement (\b): words that contain profane substrings
    /// must NOT trigger false positives (e.g. "class" contains "ass", "hello" contains "hell").
    /// </summary>
    [Theory]
    [InlineData("this is a class act")]
    [InlineData("please pass the salt")]
    [InlineData("the grass is green")]
    [InlineData("she collected seashells on the beach")]
    [InlineData("hello everyone in the chat room")]
    [InlineData("peacock feathers are colorful")]
    [InlineData("enjoying a tropical cocktail")]
    [InlineData("reading charles dickens")]
    [InlineData("the executive assistant arrived")]
    [InlineData("metal scrap collection completed")]
    public void ContainsProfanity_CleanWordsContainingProfaneSubstrings_ReturnsFalse(string cleanSentence)
    {
        Assert.False(_filter.ContainsProfanity(cleanSentence),
            $"False positive: '{cleanSentence}' was incorrectly flagged as profane.");
        Assert.Equal(cleanSentence, _filter.Scrub(cleanSentence));
    }

    #endregion

    #region Parameterized Punctuation, Casing, and Whitespace ([Theory] with [InlineData])

    [Theory]
    [InlineData("you are an idiot!", "you are an ***!", true)]
    [InlineData("what?hell!no", "what?***!no", true)]
    [InlineData("(shit)", "(***)", true)]
    [InlineData("damn...really?", "***...really?", true)]
    [InlineData("\"fuck\"", "\"***\"", true)]
    [InlineData("[bitch]", "[***]", true)]
    [InlineData("purely clean text.", "purely clean text.", false)]
    public void Scrub_WithAttachedPunctuation_ScrubsProfanityCorrectly(
        string input,
        string expectedScrubbed,
        bool expectedContains)
    {
        Assert.Equal(expectedContains, _filter.ContainsProfanity(input));
        Assert.Equal(expectedScrubbed, _filter.Scrub(input));
    }

    [Theory]
    [InlineData("dAmN", "***")]
    [InlineData("sHiT", "***")]
    [InlineData("fUcK", "***")]
    [InlineData("HeLL", "***")]
    [InlineData("CrAp", "***")]
    public void Scrub_MixedCasing_ReplacedWithAsterisks(string input, string expected)
    {
        Assert.True(_filter.ContainsProfanity(input));
        Assert.Equal(expected, _filter.Scrub(input));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("\t\t", "\t\t")]
    public void Scrub_NullOrWhitespaceInputs_PreservedAsIs(string? input, string? expected)
    {
        Assert.False(_filter.ContainsProfanity(input!));
        Assert.Equal(expected, _filter.Scrub(input!));
    }

    #endregion
}
