namespace Sedstep.Tests;

public class ParserTests
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

    private const string ThousandAscii =
        HundredAscii + HundredAscii + HundredAscii + HundredAscii + HundredAscii
        + HundredAscii + HundredAscii + HundredAscii + HundredAscii + HundredAscii;

    private const string EighteenHundredAscii =
        ThousandAscii + HundredAscii + HundredAscii + HundredAscii + HundredAscii
        + HundredAscii + HundredAscii + HundredAscii + HundredAscii;

    private const string NinetyEightAscii =
        "aaaaaaaaaa" + "aaaaaaaaaa" + "aaaaaaaaaa" + "aaaaaaaaaa" + "aaaaaaaaaa"
        + "aaaaaaaaaa" + "aaaaaaaaaa" + "aaaaaaaaaa" + "aaaaaaaaaa" + "aaaaaaaa";

    private const string BoundaryBody = EighteenHundredAscii + HundredSupplementary + NinetyEightAscii;
    private const string BoundaryScript = "a " + BoundaryBody;
    private const string TooLongBeforeSyntaxScript = "?" + BoundaryScript;
    [Fact]
    public void PublicCommandModelExposesImmutableDefaultValuesAndRecords()
    {
        Addr address = new(AddrKind.Line, 7, string.Empty, null);
        ReplPart literal = new(-1, "text");
        ReplPart wholeMatch = new(0, string.Empty);
        Cmd command = new('p', 1);

        Assert.Equal(AddrKind.Line, address.Kind);
        Assert.Equal(7, address.A);
        Assert.Equal(string.Empty, address.Source);
        Assert.Null(address.Pattern);
        Assert.Equal(new ReplPart(-1, "text"), literal);
        Assert.Equal(new ReplPart(0, string.Empty), wholeMatch);
        Assert.Equal('p', command.Letter);
        Assert.Equal(1, command.Number);
        Assert.Null(command.First);
        Assert.Null(command.Second);
        Assert.False(command.Negated);
        Assert.Equal(string.Empty, command.Label);
        Assert.Equal(-1, command.Target);
        Assert.Equal(-1, command.Jump);
        Assert.Null(command.Regex);
        Assert.Equal(string.Empty, command.RegexSource);
        Assert.Empty(command.Repl);
        Assert.Equal(string.Empty, command.ReplSource);
        Assert.False(command.Global);
        Assert.False(command.Print);
        Assert.Equal(0, command.Count);
        Assert.Equal(string.Empty, command.Text);
    }

    [Fact]
    public void CommandCopiesReplacementPartsAndExposesReadOnlyCollections()
    {
        List<ReplPart> supplied = [new ReplPart(-1, "first")];
        Cmd command = new('s', 1, repl: supplied);
        supplied[0] = new ReplPart(-1, "changed");
        supplied.Add(new ReplPart(0, "later"));

        Assert.Equal(new ReplPart[] { new(-1, "first") }, command.Repl);
        Assert.Throws<NotSupportedException>(() => ((IList<ReplPart>)command.Repl)[0] = new ReplPart(-1, "mutated"));
        Assert.Throws<NotSupportedException>(() => ((IList<ReplPart>)command.Repl).Add(new ReplPart(-1, "mutated")));
    }

    [Fact]
    public void ParseAssignsCommandNumbersAndResolvesBlockAndLabelIndexes()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse(":a;{p;{d}};b a;t;T a");

        Assert.Equal(new char[] { ':', '{', 'p', '{', 'd', '}', '}', 'b', 't', 'T' }, commands.Select(command => command.Letter));
        Assert.Equal(new int[] { 1, 2, 3, 4, 5, 0, 0, 6, 7, 8 }, commands.Select(command => command.Number));
        Assert.Equal(6, commands[1].Jump);
        Assert.Equal(5, commands[3].Jump);
        Assert.Equal("a", commands[0].Label);
        Assert.Equal("a", commands[7].Label);
        Assert.Equal(0, commands[7].Target);
        Assert.Equal(-1, commands[8].Target);
        Assert.Equal("a", commands[9].Label);
        Assert.Equal(0, commands[9].Target);
    }

    [Fact]
    public void ParsingReturnsAnImmutableCommandListAndDoesNotChangeEarlierResults()
    {
        IReadOnlyList<Cmd> earlier = ScriptParser.Parse(":kept;b kept");
        IReadOnlyList<Cmd> later = ScriptParser.Parse(":other;p");

        Assert.NotSame(earlier, later);
        Assert.Equal(new char[] { ':', 'b' }, earlier.Select(command => command.Letter));
        Assert.Equal("kept", earlier[0].Label);
        Assert.Equal(0, earlier[1].Target);
        Assert.Throws<NotSupportedException>(() => ((IList<Cmd>)earlier)[0] = later[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<Cmd>)earlier).Add(later[0]));
    }

    [Fact]
    public void ParseReturnsNoCommandsForEmptyOrSeparatorOnlyScripts()
    {
        Assert.Empty(ScriptParser.Parse(string.Empty));
        Assert.Empty(ScriptParser.Parse(" \t;\n ;\t"));
    }

    [Fact]
    public void ParseSkipsSeparatorsAndNumbersEverySimpleCommand()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse("\t;p\nD;P;n;N;g;G;h;H;x;=;q;Q;d");

        Assert.Equal(new char[] { 'p', 'D', 'P', 'n', 'N', 'g', 'G', 'h', 'H', 'x', '=', 'q', 'Q', 'd' }, commands.Select(command => command.Letter));
        Assert.Equal(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }, commands.Select(command => command.Number));
    }

    [Theory]
    [InlineData("1p", AddrKind.Line, 1, "", null, 0, "")]
    [InlineData("$p", AddrKind.Last, 0, "", null, 0, "")]
    [InlineData("/x/p", AddrKind.Regex, 0, "x", null, 0, "")]
    [InlineData("1,2p", AddrKind.Line, 1, "", AddrKind.Line, 2, "")]
    [InlineData("1,$p", AddrKind.Line, 1, "", AddrKind.Last, 0, "")]
    [InlineData("1,/x/p", AddrKind.Line, 1, "", AddrKind.Regex, 0, "x")]
    [InlineData("/x/,+0p", AddrKind.Regex, 0, "x", AddrKind.Plus, 0, "")]
    [InlineData("0,/x/p", AddrKind.Line, 0, "", AddrKind.Regex, 0, "x")]
    [InlineData("999999,+999999p", AddrKind.Line, 999999, "", AddrKind.Plus, 999999, "")]
    public void ParseSupportsAllAddressKindsAndBoundaryNumbers(
        string script,
        AddrKind firstKind,
        int firstNumber,
        string firstSource,
        AddrKind? secondKind = null,
        int secondNumber = 0,
        string secondSource = "")
    {
        Cmd command = Assert.Single(ScriptParser.Parse(script));

        Assert.Equal('p', command.Letter);
        AssertAddress(command.First, firstKind, firstNumber, firstSource);
        if (secondKind is null)
        {
            Assert.Null(command.Second);
        }
        else
        {
            AssertAddress(command.Second, secondKind.Value, secondNumber, secondSource);
        }
    }

    [Fact]
    public void ParseCompilesRegexAddressesAndPreservesUnescapedBackslashPairs()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse("/x/p;/\\t/p");

        Assert.Equal(2, commands.Count);
        AssertAddress(commands[0].First, AddrKind.Regex, 0, "x");
        Addr firstRegexAddress = Assert.IsType<Addr>(commands[0].First);
        Pattern compiledPattern = Assert.IsType<Pattern>(firstRegexAddress.Pattern);
        Found? match = compiledPattern.Search("x", 0);
        Assert.NotNull(match);
        Assert.Equal(0, match.Start);
        Assert.Equal(1, match.End);
        AssertAddress(commands[1].First, AddrKind.Regex, 0, "\\t");
        Assert.NotNull(commands[1].First!.Pattern);
    }

    [Fact]
    public void ParsePreservesTheFirstAndSecondAddressesWithNegation()
    {
        Cmd command = Assert.Single(ScriptParser.Parse("2,4 ! p"));

        AssertAddress(command.First, AddrKind.Line, 2, string.Empty);
        AssertAddress(command.Second, AddrKind.Line, 4, string.Empty);
        Assert.True(command.Negated);
    }

    [Fact]
    public void ParseResolvesTheExactAcceptedAddressSequence()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse("1p;$p;/x/p;1,2p;1,$p;1,/x/p;/x/,+0p;0,/x/p;999999,+999999p");

        Assert.Equal(9, commands.Count);
        AssertAddress(commands[0].First, AddrKind.Line, 1, string.Empty);
        AssertAddress(commands[1].First, AddrKind.Last, 0, string.Empty);
        AssertAddress(commands[2].First, AddrKind.Regex, 0, "x");
        AssertAddress(commands[3].First, AddrKind.Line, 1, string.Empty);
        AssertAddress(commands[3].Second, AddrKind.Line, 2, string.Empty);
        AssertAddress(commands[4].First, AddrKind.Line, 1, string.Empty);
        AssertAddress(commands[4].Second, AddrKind.Last, 0, string.Empty);
        AssertAddress(commands[5].First, AddrKind.Line, 1, string.Empty);
        AssertAddress(commands[5].Second, AddrKind.Regex, 0, "x");
        AssertAddress(commands[6].First, AddrKind.Regex, 0, "x");
        AssertAddress(commands[6].Second, AddrKind.Plus, 0, string.Empty);
        AssertAddress(commands[7].First, AddrKind.Line, 0, string.Empty);
        AssertAddress(commands[7].Second, AddrKind.Regex, 0, "x");
        AssertAddress(commands[8].First, AddrKind.Line, 999999, string.Empty);
        AssertAddress(commands[8].Second, AddrKind.Plus, 999999, string.Empty);
        Assert.NotNull(commands[2].First!.Pattern);
        Assert.NotNull(commands[5].Second!.Pattern);
        Assert.NotNull(commands[6].First!.Pattern);
        Assert.NotNull(commands[7].Second!.Pattern);
    }

    [Theory]
    [InlineData("1,", "expected an address after , at line 1 column 3")]
    [InlineData("1,+p", "expected a number after + at line 1 column 4")]
    [InlineData("1,3", "missing command at line 1 column 4")]
    [InlineData("0p", "line address 0 is only valid as 0,/regex/ at line 1 column 1")]
    [InlineData("1000000p", "number is larger than 999999 at line 1 column 7")]
    [InlineData("/x", "unterminated address regex at line 1 column 1")]
    [InlineData("!p", "! needs an address at line 1 column 1")]
    [InlineData("1!!p", "multiple ! at line 1 column 3")]
    [InlineData("1}", "} takes no address at line 1 column 2")]
    [InlineData("1:a", ": takes no address at line 1 column 2")]
    [InlineData("1,2q", "q and Q take at most one address at line 1 column 1")]
    [InlineData("1,2Q", "q and Q take at most one address at line 1 column 1")]
    public void ParseReportsExactAddressSyntaxFailures(string script, string expectedMessage)
    {
        Failure failure = Assert.Throws<Failure>(() => ScriptParser.Parse(script));

        Assert.Equal(expectedMessage, failure.Message);
    }

    [Fact]
    public void ParseResolvesForwardAndBackwardBranchTargetsAndBareTargets()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse("b later;:later;t later;T");

        Assert.Equal('b', commands[0].Letter);
        Assert.Equal("later", commands[0].Label);
        Assert.Equal(1, commands[0].Target);
        Assert.Equal(':', commands[1].Letter);
        Assert.Equal("later", commands[1].Label);
        Assert.Equal(1, commands[2].Target);
        Assert.Equal(-1, commands[3].Target);
    }

    [Fact]
    public void ParseAcceptsAsciiLabelNamesAndResolvesLabelsWithoutBranchSpacing()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse(":A_9;bA_9");

        Assert.Equal("A_9", commands[0].Label);
        Assert.Equal("A_9", commands[1].Label);
        Assert.Equal(0, commands[1].Target);
    }

    [Theory]
    [InlineData(":", "missing label name at line 1 column 2")]
    [InlineData(":a;:a", "duplicate label a at line 1 column 5")]
    [InlineData("b nowhere", "can't find label nowhere at line 1 column 3")]
    [InlineData("b z;b a", "can't find label z at line 1 column 3")]
    [InlineData("b z;k", "unknown command \"k\" at line 1 column 5")]
    [InlineData("p;p;}", "unexpected } at line 1 column 5")]
    [InlineData("{p", "unmatched { at line 1 column 1")]
    [InlineData("{ {p", "unmatched { at line 1 column 3")]
    [InlineData("pp", "extra characters after command at line 1 column 2")]
    [InlineData("#", "unknown command \"#\" at line 1 column 1")]
    [InlineData("{b nowhere", "unmatched { at line 1 column 1")]
    public void ParseReportsExactLabelBlockAndCommandFailures(string script, string expectedMessage)
    {
        Failure failure = Assert.Throws<Failure>(() => ScriptParser.Parse(script));

        Assert.Equal(expectedMessage, failure.Message);
    }

    [Fact]
    public void ParseRetainsTextCommandPunctuationUntilTheEndOfItsLine()
    {
        Cmd command = Assert.Single(ScriptParser.Parse("a x;y}"));

        Assert.Equal('a', command.Letter);
        Assert.Equal("x;y}", command.Text);
    }

    [Fact]
    public void ParseSkipsTextCommandBlanksAndContinuesAfterItsLine()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse("i \t hello\np");

        Assert.Equal(2, commands.Count);
        Assert.Equal('i', commands[0].Letter);
        Assert.Equal("hello", commands[0].Text);
        Assert.Equal('p', commands[1].Letter);
    }

    [Theory]
    [InlineData("a", "expected text after a or i at line 1 column 2")]
    [InlineData("i   ", "expected text after a or i at line 1 column 5")]
    [InlineData("a x\\y", "backslash in text is not supported at line 1 column 4")]
    public void ParseReportsExactTextCommandFailures(string script, string expectedMessage)
    {
        Failure failure = Assert.Throws<Failure>(() => ScriptParser.Parse(script));

        Assert.Equal(expectedMessage, failure.Message);
    }

    [Theory]
    [InlineData("s/a/b/", "a", "b")]
    [InlineData("s,a,b,", "a", "b")]
    [InlineData("s:a:b:", "a", "b")]
    [InlineData("s#a#b#", "a", "b")]
    [InlineData("s@a@b@", "a", "b")]
    [InlineData("s%a%b%", "a", "b")]
    [InlineData("s~a~b~", "a", "b")]
    [InlineData("s!a!b!", "a", "b")]
    [InlineData("s_a_b_", "a", "b")]
    [InlineData("s=a=b=", "a", "b")]
    public void ParseSupportsEverySubstitutionDelimiter(string script, string regexSource, string replacementSource)
    {
        Cmd command = Assert.Single(ScriptParser.Parse(script));

        Assert.Equal('s', command.Letter);
        Assert.Equal(regexSource, command.RegexSource);
        Assert.Equal(replacementSource, command.ReplSource);
        Assert.Equal(0, Assert.IsType<Pattern>(command.Regex).Groups);
        Assert.Equal(new ReplPart(-1, replacementSource), Assert.Single(command.Repl));
    }

    [Fact]
    public void ParseInterpretsWholeMatchCapturesAndEscapedReplacementLiterals()
    {
        Cmd command = Assert.Single(ScriptParser.Parse("s/(a)(b)?/&\\1\\n\\t\\!/"));

        Assert.Equal("(a)(b)?", command.RegexSource);
        Assert.Equal(2, Assert.IsType<Pattern>(command.Regex).Groups);
        Assert.Equal("&\\1\\n\\t\\!", command.ReplSource);
        Assert.Equal(3, command.Repl.Count);
        Assert.Equal(new ReplPart(0, string.Empty), command.Repl[0]);
        Assert.Equal(new ReplPart(1, string.Empty), command.Repl[1]);
        Assert.Equal(new ReplPart(-1, "\n\t!"), command.Repl[2]);
    }

    [Theory]
    [InlineData("s/a/b/pg2", 2, true, true)]
    [InlineData("s/a/b/999", 999, false, false)]
    public void ParseAcceptsSubstitutionFlagOrderAndMaximumCount(string script, int count, bool global, bool print)
    {
        Cmd command = Assert.Single(ScriptParser.Parse(script));

        Assert.Equal(count, command.Count);
        Assert.Equal(global, command.Global);
        Assert.Equal(print, command.Print);
        Assert.Equal(new ReplPart(-1, "b"), Assert.Single(command.Repl));
    }

    [Fact]
    public void ParseDecodesEscapedDelimitersInRegexAndReplacementSources()
    {
        Cmd command = Assert.Single(ScriptParser.Parse("s/a\\/b/c\\/d/"));

        Assert.Equal("a/b", command.RegexSource);
        Assert.Equal("c/d", command.ReplSource);
        Assert.Equal(0, Assert.IsType<Pattern>(command.Regex).Groups);
        Assert.Equal(new ReplPart(-1, "c/d"), Assert.Single(command.Repl));
    }

    [Fact]
    public void ParsePreservesEscapedPhysicalLfInReplacementSourceAndInterpretsItAsNewline()
    {
        Cmd command = Assert.Single(ScriptParser.Parse("s/a/\\\nb/"));

        Assert.Equal("a", command.RegexSource);
        Assert.Equal("\\nb", command.ReplSource);
        Assert.Equal(new ReplPart(-1, "\nb"), Assert.Single(command.Repl));
    }

    [Theory]
    [InlineData("s/(a)/&\\1\\n\\t\\0/", "unsupported escape \\0 at line 1 column 14")]
    [InlineData("s/(a)/&\\1\\n\\t  \\0/", "unsupported escape \\0 at line 1 column 16")]
    public void ParseReportsUnsupportedZeroReplacementEscapeAtItsOriginalBackslash(string script, string expectedMessage)
    {
        Failure failure = Assert.Throws<Failure>(() => ScriptParser.Parse(script));

        Assert.Equal(expectedMessage, failure.Message);
    }

    [Theory]
    [InlineData("s/a/b", "unterminated s command at line 1 column 1")]
    [InlineData("s;a;b;", "invalid delimiter \";\" at line 1 column 2")]
    [InlineData("s/a/b/x", "unknown s flag \"x\" at line 1 column 7")]
    [InlineData("s/a/b/gg", "multiple g flags at line 1 column 8")]
    [InlineData("s/a/b/pp", "multiple p flags at line 1 column 8")]
    [InlineData("s/a/b/1g2", "multiple number flags at line 1 column 9")]
    [InlineData("s/a/b/0", "the number flag may not be zero at line 1 column 7")]
    [InlineData("s/a/b/1000", "number is larger than 999 at line 1 column 10")]
    [InlineData("s/a/\\1/", "invalid reference \\1 on s command's RHS at line 1 column 5")]
    [InlineData("s/a/\\q/", "unsupported escape \\q at line 1 column 5")]
    [InlineData("s/(a/b/", "unmatched ( at line 1 column 3")]
    [InlineData("s/[[:foo:]]/c/", "unknown character class foo at line 1 column 3")]
    [InlineData("99999999999999999999p", "number is larger than 999999 at line 1 column 7")]
    public void ParseReportsExactSubstitutionAndLongNumberFailures(string script, string expectedMessage)
    {
        Failure failure = Assert.Throws<Failure>(() => ScriptParser.Parse(script));

        Assert.Equal(expectedMessage, failure.Message);
    }

    [Fact]
    public void ParseAcceptsTheTwoThousandCodePointBoundaryWithSupplementaryText()
    {
        Cmd command = Assert.Single(ScriptParser.Parse(BoundaryScript));

        Assert.Equal('a', command.Letter);
        Assert.Equal(BoundaryBody, command.Text);
    }

    [Fact]
    public void ParseChecksTheTwoThousandAndOneCodePointLimitBeforeSyntax()
    {
        Failure failure = Assert.Throws<Failure>(() => ScriptParser.Parse(TooLongBeforeSyntaxScript));

        Assert.Equal("the script is longer than 2000 characters", failure.Message);
    }

    [Theory]
    [InlineData("a 😀\np;k", "unknown command \"k\" at line 2 column 3")]
    [InlineData("s/😀/x/z", "unknown s flag \"z\" at line 1 column 7")]
    [InlineData("/(/p", "unmatched ( at line 1 column 2")]
    [InlineData("😀", "unknown command \"?\" at line 1 column 1")]
    public void ParseReportsExactCodePointColumnsForSupplementaryCharacters(string script, string expectedMessage)
    {
        Failure failure = Assert.Throws<Failure>(() => ScriptParser.Parse(script));

        Assert.Equal(expectedMessage, failure.Message);
    }

    [Fact]
    public void ParsePreservesAnIsolatedUtf16SurrogateInTextCommands()
    {
        const string isolatedSurrogate = "\uD800";
        Cmd command = Assert.Single(ScriptParser.Parse("a " + isolatedSurrogate));

        Assert.Equal(isolatedSurrogate, command.Text);
    }

    [Fact]
    public void PublicScriptContractsUseTheSedstepNamespace()
    {
        Assert.Equal("Sedstep", typeof(AddrKind).Namespace);
        Assert.Equal("Sedstep", typeof(Addr).Namespace);
        Assert.Equal("Sedstep", typeof(ReplPart).Namespace);
        Assert.Equal("Sedstep", typeof(Cmd).Namespace);
        Assert.Equal("Sedstep", typeof(ScriptParser).Namespace);
        Assert.Equal("Sedstep", typeof(ScriptText).Namespace);
    }

    [Theory]
    [InlineData(" 002,004 ! p ", "2,4!p")]
    [InlineData("0,/x/p", "0,/x/p")]
    [InlineData("/x/,+01i bye", "/x/,+1i bye")]
    [InlineData("$ ! b a;:a", "$!b a|:a")]
    [InlineData("t;T;b", "t|T|b")]
    [InlineData("{p}", "{|p|}")]
    [InlineData("s#a/b#c/d#pg02", "s/a\\/b/c\\/d/2gp")]
    [InlineData("s/\\n/\\t/", "s/\\n/\\t/")]
    [InlineData("/a\\/b/p", "/a\\/b/p")]
    [InlineData("a x;y}", "a x;y}")]
    public void ScriptTextRendersCommandsInCanonicalForm(string script, string expected)
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse(script);
        string actual = string.Join("|", commands.Select(ScriptText.Of));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ScriptTextPreservesBackslashPairsEscapesOtherSlashesAndDoesNotMutateCommands()
    {
        Cmd command = Assert.Single(ScriptParser.Parse("s#left\\t/a#right\\n/b#gp2"));
        string regexSource = command.RegexSource;
        string replacementSource = command.ReplSource;
        IReadOnlyList<ReplPart> replacement = command.Repl;

        Assert.Equal("s/left\\t\\/a/right\\n\\/b/2gp", ScriptText.Of(command));
        Assert.Equal(regexSource, command.RegexSource);
        Assert.Equal(replacementSource, command.ReplSource);
        Assert.Equal(replacement, command.Repl);
        Assert.Equal(new ReplPart(-1, "right\n/b"), Assert.Single(command.Repl));
    }

    [Fact]
    public void ScriptTextPreservesSlashPairsAlreadyPresentInCommandSources()
    {
        Cmd command = new('s', 1, regexSource: "a\\/b/c", replSource: "d\\/e/f");

        Assert.Equal("s/a\\/b\\/c/d\\/e\\/f/", ScriptText.Of(command));
        Assert.Equal("a\\/b/c", command.RegexSource);
        Assert.Equal("d\\/e/f", command.ReplSource);
    }

    private static void AssertAddress(Addr? actual, AddrKind kind, int number, string source)
    {
        Assert.NotNull(actual);
        Assert.Equal(kind, actual.Kind);
        Assert.Equal(number, actual.A);
        Assert.Equal(source, actual.Source);
    }
}
