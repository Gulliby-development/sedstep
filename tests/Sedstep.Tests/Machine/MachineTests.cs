namespace Sedstep.Tests;

public class MachineTests
{
    private const string HundredAs =
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa";

    private const string NineThousandNineHundredAs =
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs +
        HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs + HundredAs;

    private const string NinetyNineAs =
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaaa" +
        "aaaaaaaaa";

    private const string TenThousandCodePoints = NineThousandNineHundredAs + NinetyNineAs + "😀";
    private const string TenThousandOneCodePoints = NineThousandNineHundredAs + HundredAs + "😀";
    private const string NineThousandNineHundredNinetyNineAs = NineThousandNineHundredAs + NinetyNineAs;
    private const string TenLines =
        "a\n" +
        "a\n" +
        "a\n" +
        "a\n" +
        "a\n" +
        "a\n" +
        "a\n" +
        "a\n" +
        "a\n" +
        "a\n";

    private const string HundredLines =
        TenLines + TenLines + TenLines + TenLines + TenLines + TenLines + TenLines + TenLines + TenLines + TenLines;

    private const string ThousandLines =
        HundredLines + HundredLines + HundredLines + HundredLines + HundredLines + HundredLines + HundredLines + HundredLines + HundredLines + HundredLines;

    private const string TenThousandLines =
        ThousandLines + ThousandLines + ThousandLines + ThousandLines + ThousandLines + ThousandLines + ThousandLines + ThousandLines + ThousandLines + ThousandLines;

    private const string ExactStepScript = "h;h;h;h;h;h;h;h;h;h";

    [Fact]
    public void ReusingParsedCommandsKeepsExecutionsIndependentAndTraceDoesNotChangeResults()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse("0,/x/p");

        Execution first = Machine.Execute(commands, "x\ny\n", quiet: true, trace: false);
        Execution second = Machine.Execute(commands, "x\ny\n", quiet: true, trace: true);

        Assert.Equal("x\n", first.Output);
        Assert.Equal("x\n", second.Output);
        Assert.Equal(1, first.Steps);
        Assert.Equal(1, second.Steps);
        Assert.Equal(2, first.Cycles);
        Assert.Equal(2, second.Cycles);
        Assert.Empty(first.Rows);
        Assert.Equal(3, second.Rows.Count);
        Assert.Same(commands[0], second.Rows[0].Command);
    }

    [Theory]
    [InlineData("p", "", "")]
    [InlineData("p", "a\nb", "a\na\nb\nb")]
    [InlineData("s/x/[&]/;a end", "x\n", "[x]\nend\n")]
    [InlineData("i before\na after", "x", "before\nx\nafter\n")]
    public void RunWritesPendingLineFeedsAndQueuedOutput(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: false);

        Assert.Equal(expectedOutput, execution.Output);
    }

    [Theory]
    [InlineData("p", "a\r\n", "a\r\n")]
    [InlineData("P", "x", "x")]
    [InlineData("N;P", "a\nb", "a\n")]
    [InlineData("p;=", "x", "x\n1\n")]
    public void QuietRunPreservesLineEndingsAndOrdersWrites(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: true);

        Assert.Equal(expectedOutput, execution.Output);
    }

    [Theory]
    [InlineData(":a;N;$!ba;s/\\n/,/g", "one\ntwo\nthree\nfour\n", "one,two,three,four\n")]
    [InlineData("1!G;h;$!d", "one\ntwo\nthree\nfour\n", "four\nthree\ntwo\none\n")]
    [InlineData("$!N;$!D", "one\ntwo\nthree\nfour\n", "three\nfour\n")]
    [InlineData("n", "a\nb\nc\n", "a\nb\nc\n")]
    [InlineData("N", "a\nb", "a\nb")]
    [InlineData("D", "x\n", "")]
    [InlineData("a tail\nq", "x", "x\ntail\n")]
    public void RunHandlesCycleCommandsAndSuppliedExamples(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: false);

        Assert.Equal(expectedOutput, execution.Output);
    }

    [Theory]
    [InlineData("1{p};2p", "a\nb\n", "a\nb\n")]
    [InlineData("s/x/x/;t yes;d;:yes;p", "x\n", "x\n")]
    [InlineData("s/x/y/;t yes;:yes;t;p", "x\n", "y\n")]
    [InlineData("s/x/y/;s/z/w/;t yes;d;:yes;p", "x\n", "y\n")]
    [InlineData("s/a/A/;n;t;p", "a\nb\n", "b\n")]
    [InlineData("p;a tail\nQ", "x", "x")]
    [InlineData("s/x/y/;T;T;p", "x\n", "")]
    public void QuietRunHandlesBlocksSubstitutionFlagsReadsAndQuit(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: true);

        Assert.Equal(expectedOutput, execution.Output);
    }

    [Theory]
    [InlineData("g", "x", "\n")]
    [InlineData("G", "x", "x\n\n")]
    [InlineData("H;g", "x", "\nx")]
    [InlineData("h;G", "x", "x\nx")]
    public void RunPreservesHoldSpaceTextAndEndOfLineFlags(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: false);

        Assert.Equal(expectedOutput, execution.Output);
    }

    [Theory]
    [InlineData("h;g;p", "😀", "😀")]
    [InlineData("x;p;x;p", "x", "\nx")]
    public void QuietRunCopiesUnicodeAndSwapsBothEndOfLineFlags(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: true);

        Assert.Equal(expectedOutput, execution.Output);
    }

    [Theory]
    [InlineData("2,3p", "one\ntwo\nthree\nfour\n", "two\nthree\n")]
    [InlineData("3,1p", "a\nb\nc\nd\n", "c\n")]
    [InlineData("0,/x/p", "x\ny\n", "x\n")]
    [InlineData("/x/,/x/p", "x\ny\nx\nz\n", "x\ny\nx\n")]
    [InlineData("/x/,+0p", "x\ny\nx\n", "x\nx\n")]
    [InlineData("/x/,+1p", "x\ny\nz\n", "x\ny\n")]
    [InlineData("2,$p", "a\nb\nc\n", "b\nc\n")]
    [InlineData("2,3!p", "a\nb\nc\nd\n", "a\nd\n")]
    [InlineData("N;2,3p", "a\nb\nc\nd\n", "a\nb\n")]
    [InlineData("N;1,1p", "a\nb\n", "")]
    public void RunSelectsClosedAndRelativeRangesAcrossReads(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: true);

        Assert.Equal(expectedOutput, execution.Output);
    }

    [Theory]
    [InlineData("s/(o+)-(b)/\\2-\\1/", "foo-bar\n", "fb-ooar\n")]
    [InlineData("s/./Y/", "😀x\n", "Yx\n")]
    [InlineData("s/a/X/2", "aaa\n", "aXa\n")]
    [InlineData("s/a/X/2g", "aaa\n", "aXX\n")]
    [InlineData("s/a/X/g", "aaa\n", "XXX\n")]
    [InlineData("s/a/X/4p", "aaa\n", "aaa\n")]
    [InlineData("s/(a)?b/<\\1>/", "b\n", "<>\n")]
    [InlineData("s/x/[&]/p", "x", "[x]\n[x]")]
    public void RunAppliesCapturesNumberedGlobalAndPrintedSubstitutions(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: false);

        Assert.Equal(expectedOutput, execution.Output);
    }

    [Theory]
    [InlineData("s/a*/X/g", "ab\n", "XbX\n")]
    [InlineData("s/a*/X/2", "ab\n", "abX\n")]
    [InlineData("s/()/X/g", "😀x\n", "X😀XxX\n")]
    [InlineData("s/a*/X/g", "\n", "X\n")]
    [InlineData("s/a*/X/g", "aa\n", "X\n")]
    public void RunSuppressesAdjacentEmptyMatchesAndAdvancesByCodePoint(
        string script,
        string text,
        string expectedOutput)
    {
        Execution execution = Execute(script, text, quiet: false);

        Assert.Equal(expectedOutput, execution.Output);
    }


    [Fact]
    public void ReadAndHoldSpacesAcceptExactlyTenThousandCodePointsIncludingSupplementaryText()
    {
        Assert.Equal(10001, TenThousandCodePoints.Length);
        Assert.EndsWith("😀", TenThousandCodePoints);

        Execution execution = Machine.Execute(
            ScriptParser.Parse("h"),
            TenThousandCodePoints,
            quiet: true,
            trace: true);

        Assert.Equal(1, execution.Steps);
        Assert.Equal(TenThousandCodePoints, execution.Rows[0].Pattern);
        Assert.Equal(TenThousandCodePoints, execution.Rows[0].Hold);
    }

    [Fact]
    public void ReadRejectsTenThousandAndOneCodePointsIncludingSupplementaryText()
    {
        Assert.Equal(10002, TenThousandOneCodePoints.Length);
        Assert.EndsWith("😀", TenThousandOneCodePoints);

        Failure failure = Assert.Throws<Failure>(() =>
            Machine.Execute(
                ScriptParser.Parse("p"),
                TenThousandOneCodePoints,
                quiet: true,
                trace: false));

        AssertMachineFailure(failure, "the pattern space is larger than 10000 characters");
    }

    [Fact]
    public void PatternAndHoldAppendCommandsAcceptExactlyTenThousandCodePoints()
    {
        Assert.Equal(9999, NineThousandNineHundredNinetyNineAs.Length);

        Execution patternExecution = Machine.Execute(
            ScriptParser.Parse("G"),
            NineThousandNineHundredNinetyNineAs,
            quiet: true,
            trace: true);
        Execution holdExecution = Machine.Execute(
            ScriptParser.Parse("H"),
            NineThousandNineHundredNinetyNineAs,
            quiet: true,
            trace: true);

        Assert.Equal(10000, patternExecution.Rows[0].Pattern.Length);
        Assert.Equal(10000, holdExecution.Rows[0].Hold.Length);
    }

    [Fact]
    public void PatternAndHoldCommandsRejectOneCharacterBeyondTheirSpaceLimit()
    {
        Failure patternFailure = Assert.Throws<Failure>(() =>
            Machine.Execute(
                ScriptParser.Parse("G"),
                TenThousandCodePoints,
                quiet: true,
                trace: false));
        Failure holdFailure = Assert.Throws<Failure>(() =>
            Machine.Execute(
                ScriptParser.Parse("H"),
                TenThousandCodePoints,
                quiet: true,
                trace: false));

        AssertMachineFailure(patternFailure, "the pattern space is larger than 10000 characters");
        AssertMachineFailure(holdFailure, "the hold space is larger than 10000 characters");
    }

    [Theory]
    [InlineData(":a;ba", "the command limit of 100000 is exceeded")]
    [InlineData(":a;H;G;ba", "the pattern space is larger than 10000 characters")]
    [InlineData(":a;H;ba", "the hold space is larger than 10000 characters")]
    public void RunStopsBranchAndSpaceGrowthLoopsWithUnprefixedFailures(string script, string message)
    {
        Failure failure = Assert.Throws<Failure>(() =>
            Machine.Execute(
                ScriptParser.Parse(script),
                "x",
                quiet: true,
                trace: false));

        AssertMachineFailure(failure, message);
    }

    [Fact]
    public void ExactlyOneHundredThousandSelectedCommandsSucceed()
    {
        Execution execution = Machine.Execute(
            ScriptParser.Parse(ExactStepScript),
            TenThousandLines,
            quiet: true,
            trace: false);

        Assert.Equal(100000, execution.Steps);
        Assert.Equal(10000, execution.Cycles);
        Assert.Empty(execution.Output);
    }

    [Fact]
    public void TheCommandAfterOneHundredThousandSelectedCommandsFailsBeforeItRuns()
    {
        Failure failure = Assert.Throws<Failure>(() =>
            Machine.Execute(
                ScriptParser.Parse(ExactStepScript + ";$h"),
                TenThousandLines,
                quiet: true,
                trace: false));

        AssertMachineFailure(failure, "the command limit of 100000 is exceeded");
    }

    [Fact]
    public void SuccessfulAppendNextResetsSubstitutionFlagForT()
    {
        Execution execution = Execute("s/a/A/;N;t;p", "a\nb\n", quiet: true);

        Assert.Equal("A\nb\n", execution.Output);
        Assert.Equal(4, execution.Steps);
    }

    [Fact]
    public void SuccessfulAppendNextResetsSubstitutionFlagForCapitalT()
    {
        Execution execution = Execute("s/a/A/;N;T done;p;:done", "a\nb\n", quiet: true);

        Assert.Empty(execution.Output);
        Assert.Equal(3, execution.Steps);
    }

    private static void AssertMachineFailure(Failure failure, string expectedMessage)
    {
        Assert.Equal(expectedMessage, failure.Message);
        Assert.False(failure.Message.StartsWith("sedstep:", StringComparison.Ordinal));
    }

    private static Execution Execute(string script, string text, bool quiet)
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse(script);
        return Machine.Execute(commands, text, quiet, trace: false);
    }
}
