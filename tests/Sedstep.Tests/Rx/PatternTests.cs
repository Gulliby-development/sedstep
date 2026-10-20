namespace Sedstep.Tests;

public class PatternTests
{
    private const string HundredAscii =
        "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa";

    private const string HundredAndOneAscii =
        "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "aaaaaaaaaa"
        + "a";

    private const string HundredSupplementary =
        "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀"
        + "😀😀😀😀😀😀😀😀😀😀";

    [Theory]
    [InlineData("", "empty regular expression")]
    [InlineData("(", "unmatched (")]
    [InlineData(")", "unmatched )")]
    [InlineData("()()()()()()()()()()", "more than 9 groups")]
    [InlineData("a|b", "alternation is not supported")]
    [InlineData("\\1", "backreferences are not supported")]
    [InlineData("\\9", "backreferences are not supported")]
    [InlineData("\\q", "unsupported escape \\q")]
    [InlineData("\\0", "unsupported escape \\0")]
    [InlineData("\\", "trailing backslash")]
    public void ParseRejectsMalformedSourcesWithExactMessages(string source, string message)
    {
        Failure failure = Assert.Throws<Failure>(() => Pattern.Parse(source));

        Assert.Equal(message, failure.Message);
    }

    [Fact]
    public void ParseAcceptsExactlyOneHundredAsciiCodePoints()
    {
        Pattern pattern = Pattern.Parse(HundredAscii);

        Assert.Equal(0, pattern.Groups);
    }

    [Fact]
    public void ParseRejectsOneHundredAndOneAsciiCodePoints()
    {
        Failure failure = Assert.Throws<Failure>(() => Pattern.Parse(HundredAndOneAscii));

        Assert.Equal("regular expression is longer than 100 characters", failure.Message);
    }

    [Fact]
    public void ParseCountsSupplementaryCharactersAsOneCodePoint()
    {
        Pattern pattern = Pattern.Parse(HundredSupplementary);

        Assert.Equal(0, pattern.Groups);
    }

    [Fact]
    public void ParseAcceptsNineCapturingGroups()
    {
        Pattern pattern = Pattern.Parse("()()()()()()()()()");

        Assert.True(pattern.Groups == 9);
    }
    [Fact]
    public void SearchExposesGroupCountAndNonparticipatingCaptures()
    {
        Pattern pattern = Pattern.Parse("(a)(b)?");
        Found? found = pattern.Search("a", 0);

        Assert.Equal(2, pattern.Groups);
        Assert.NotNull(found);
        Assert.Equal(0, found.Start);
        Assert.Equal(1, found.End);
        IReadOnlyList<(int Start, int End)?> groups = found.Groups;
        Assert.Equal(new (int Start, int End)?[] { (0, 1), null }, groups);
    }

    [Fact]
    public void SearchReturnsUtf16OffsetsForSupplementaryCharacters()
    {
        Found? found = Pattern.Parse("(😀)(x)").Search("z😀x", 0);

        Assert.NotNull(found);
        Assert.Equal(1, found.Start);
        Assert.Equal(4, found.End);
        Assert.Equal(new (int Start, int End)?[] { (1, 3), (3, 4) }, found.Groups);
    }

    [Fact]
    public void SearchReturnsNullWhenThereIsNoMatch()
    {
        Found? found = Pattern.Parse("a").Search("bbb", 0);

        Assert.Null(found);
    }

    [Theory]
    [InlineData(".", "\n", 0, 1)]
    [InlineData(".", "😀x", 0, 2)]
    [InlineData("\\n\\t", "\n\t", 0, 2)]
    [InlineData("\\.", ".", 0, 1)]
    [InlineData("\\[", "[", 0, 1)]
    [InlineData("\\]", "]", 0, 1)]
    [InlineData("\\\\", "\\", 0, 1)]
    [InlineData("\\*", "*", 0, 1)]
    [InlineData("\\+", "+", 0, 1)]
    [InlineData("\\?", "?", 0, 1)]
    [InlineData("\\(", "(", 0, 1)]
    [InlineData("\\)", ")", 0, 1)]
    [InlineData("\\{", "{", 0, 1)]
    [InlineData("\\}", "}", 0, 1)]
    [InlineData("\\^", "^", 0, 1)]
    [InlineData("\\$", "$", 0, 1)]
    [InlineData("\\|", "|", 0, 1)]
    [InlineData("\\/", "/", 0, 1)]
    public void SearchMatchesSupportedAtomsAndEscapedMetacharacters(
        string source,
        string text,
        int expectedStart,
        int expectedEnd)
    {
        Found? found = Pattern.Parse(source).Search(text, 0);

        Assert.NotNull(found);
        Assert.Equal(expectedStart, found.Start);
        Assert.Equal(expectedEnd, found.End);
    }

    [Theory]
    [InlineData("a*", "aaa", 0, 3)]
    [InlineData("a+", "aaa", 0, 3)]
    [InlineData("a?", "aaa", 0, 1)]
    [InlineData("a{2}", "aaa", 0, 2)]
    [InlineData("a{2,}", "aaa", 0, 3)]
    [InlineData("a{1,2}", "aaa", 0, 2)]
    [InlineData("a{0}", "a", 0, 0)]
    [InlineData("a{0,99}", "aaa", 0, 3)]
    public void SearchSelectsLongestExtentForRepetitionForms(
        string source,
        string text,
        int expectedStart,
        int expectedEnd)
    {
        Found? found = Pattern.Parse(source).Search(text, 0);

        Assert.NotNull(found);
        Assert.Equal(expectedStart, found.Start);
        Assert.Equal(expectedEnd, found.End);
    }

    [Fact]
    public void ParseAcceptsMaximumIntervalEndpointOfNinetyNine()
    {
        Pattern pattern = Pattern.Parse("a{99}");

        Assert.Equal(0, pattern.Groups);
    }

    [Theory]
    [InlineData("*a")]
    [InlineData("^*")]
    [InlineData("$+")]
    [InlineData("a**")]
    [InlineData("a{1}?")]
    public void ParseRejectsRepetitionWithoutARepeatableAtom(string source)
    {
        Failure failure = Assert.Throws<Failure>(() => Pattern.Parse(source));

        Assert.Equal("nothing to repeat", failure.Message);
    }

    [Theory]
    [InlineData("a{2,1}")]
    [InlineData("a{}")]
    [InlineData("a{,2}")]
    [InlineData("a{1")]
    [InlineData("a{x}")]
    [InlineData("a{1x}")]
    [InlineData("a{1,2")]
    public void ParseRejectsMalformedIntervals(string source)
    {
        Failure failure = Assert.Throws<Failure>(() => Pattern.Parse(source));

        Assert.Equal("invalid interval", failure.Message);
    }

    [Theory]
    [InlineData("a{100}")]
    [InlineData("a{1,100}")]
    public void ParseRejectsIntervalEndpointsLargerThanNinetyNine(string source)
    {
        Failure failure = Assert.Throws<Failure>(() => Pattern.Parse(source));

        Assert.Equal("interval is larger than 99", failure.Message);
    }

    [Theory]
    [InlineData("[]]", "]", 0, 1)]
    [InlineData("[-a]", "-", 0, 1)]
    [InlineData("[a-]", "-", 0, 1)]
    [InlineData("[a-c]+", "abc", 0, 3)]
    [InlineData("[^a]", "😀", 0, 2)]
    [InlineData("[😀-😁]", "😁", 0, 2)]
    [InlineData("[\\n\\t]+", "\n\t", 0, 2)]
    [InlineData("[[:alpha:]]+", "aZ", 0, 2)]
    [InlineData("[[:digit:]]+", "09", 0, 2)]
    [InlineData("[[:alnum:]]+", "aZ09", 0, 4)]
    [InlineData("[[:upper:]]+", "AZ", 0, 2)]
    [InlineData("[[:lower:]]+", "az", 0, 2)]
    [InlineData("[[:space:]]+", " \t\n\v\f\r", 0, 6)]
    public void SearchMatchesBracketItemsRangesAndAsciiClasses(
        string source,
        string text,
        int expectedStart,
        int expectedEnd)
    {
        AssertMatchExtent(source, text, expectedStart, expectedEnd);
    }

    [Theory]
    [InlineData("[[:alpha:]]", "é")]
    [InlineData("[[:digit:]]", "١")]
    public void SearchKeepsCharacterClassesAsciiOnly(string source, string text)
    {
        Assert.Null(Pattern.Parse(source).Search(text, 0));
    }

    [Theory]
    [InlineData("[", "unterminated [")]
    [InlineData("[a", "unterminated [")]
    [InlineData("[z-a]", "invalid range")]
    [InlineData("[[:foo:]]", "unknown character class foo")]
    [InlineData("[\\q]", "backslash in a bracket expression is not supported")]
    [InlineData("[\\-]", "backslash in a bracket expression is not supported")]
    public void ParseRejectsMalformedBracketExpressions(string source, string message)
    {
        Failure failure = Assert.Throws<Failure>(() => Pattern.Parse(source));

        Assert.Equal(message, failure.Message);
    }

    [Theory]
    [InlineData("^a", "ba", 1, -1, -1)]
    [InlineData("a$", "a\n", 0, -1, -1)]
    [InlineData("a^", "a", 0, -1, -1)]
    [InlineData("^$", "", 0, 0, 0)]
    [InlineData("$", "😀", 0, 2, 2)]
    [InlineData("a*", "baaa", 0, 0, 0)]
    [InlineData("a+", "baaa", 2, 2, 4)]
    [InlineData(".", "😀x", 1, 2, 3)]
    [InlineData(".", "x", -1, 0, 1)]
    [InlineData("$", "x", 2, -1, -1)]
    public void SearchHonorsAnchorsAndUtf16FromOffsets(
        string source,
        string text,
        int from,
        int expectedStart,
        int expectedEnd)
    {
        Found? found = Pattern.Parse(source).Search(text, from);

        if (expectedStart < 0)
        {
            Assert.Null(found);
            return;
        }

        Assert.NotNull(found);
        Assert.Equal(expectedStart, found.Start);
        Assert.Equal(expectedEnd, found.End);
    }

    [Fact]
    public void SearchUsesGreedyCapturesForTheLongestExtent()
    {
        Found? found = Pattern.Parse("(a*)(a*)").Search("aa", 0);

        Assert.NotNull(found);
        Assert.Equal(0, found.Start);
        Assert.Equal(2, found.End);
        Assert.Equal(new (int Start, int End)?[] { (0, 2), (2, 2) }, found.Groups);
    }

    [Fact]
    public void SearchRetainsTheLastCaptureFromARepeatedGroup()
    {
        Found? found = Pattern.Parse("(a)+").Search("aaa", 0);

        Assert.NotNull(found);
        Assert.Equal(0, found.Start);
        Assert.Equal(3, found.End);
        Assert.Equal(new (int Start, int End)?[] { (2, 3) }, found.Groups);
    }

    [Fact]
    public void SearchCapturesAnEmptyGroup()
    {
        Found? found = Pattern.Parse("()").Search("x", 0);

        Assert.NotNull(found);
        Assert.Equal(0, found.Start);
        Assert.Equal(0, found.End);
        Assert.Equal(new (int Start, int End)?[] { (0, 0) }, found.Groups);
    }

    [Fact]
    public void SearchStopsAnEmptyOptionalRepetitionAfterItsFirstIteration()
    {
        Found? found = Pattern.Parse("()*").Search("", 0);

        Assert.NotNull(found);
        Assert.Equal(0, found.Start);
        Assert.Equal(0, found.End);
        Assert.Equal(new (int Start, int End)?[] { (0, 0) }, found.Groups);
    }

    [Theory]
    [InlineData("(){2}")]
    [InlineData("(){2,3}")]
    public void SearchSatisfiesRequiredEmptyRepetitionIterations(string source)
    {
        Found? found = Pattern.Parse(source).Search("", 0);

        Assert.NotNull(found);
        Assert.Equal(0, found.Start);
        Assert.Equal(0, found.End);
        Assert.Equal(new (int Start, int End)?[] { (0, 0) }, found.Groups);
    }

    [Fact]
    public void SearchMatchesNestedRepetitionWithoutLosingTheLongestExtent()
    {
        Found? found = Pattern.Parse("(a*)*b").Search("aaab", 0);

        Assert.NotNull(found);
        Assert.Equal(0, found.Start);
        Assert.Equal(4, found.End);
    }

    [Fact]
    public void SearchTerminatesWhenNestedRepetitionCannotReachItsSuffix()
    {
        Found? found = Pattern.Parse("(a*)*b").Search("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0);

        Assert.Null(found);
    }

    [Fact]
    public void SearchPreservesIsolatedSurrogateCodeUnits()
    {
        Found? found = Pattern.Parse(".x.").Search("\uD800x\uDC00", 0);

        Assert.NotNull(found);
        Assert.Equal(0, found.Start);
        Assert.Equal(3, found.End);
    }

    private static void AssertMatchExtent(string source, string text, int expectedStart, int expectedEnd)
    {
        Found? found = Pattern.Parse(source).Search(text, 0);

        Assert.NotNull(found);
        Assert.Equal(expectedStart, found.Start);
        Assert.Equal(expectedEnd, found.End);
    }
}
