namespace Sedstep.Tests;

public class CheckTests
{
    [Fact]
    public void ReportsBranchFollowingAsUnreachable()
    {
        AssertFindings(
            "b;p",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"));
    }

    [Fact]
    public void ReportsDeleteFollowingAsUnreachable()
    {
        AssertFindings(
            "d;p",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"));
    }

    [Fact]
    public void ReportsRestartFollowingAsUnreachable()
    {
        AssertFindings(
            "D;p",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"));
    }

    [Fact]
    public void ReportsQuitFollowingAsUnreachable()
    {
        AssertFindings(
            "q;p",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"));
    }

    [Fact]
    public void ReportsSilentQuitFollowingAsUnreachable()
    {
        AssertFindings(
            "Q;p",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"));
    }

    [Fact]
    public void AttributesUnreachableCommandsToTheLatestTerminator()
    {
        AssertFindings(
            "d;q;p",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"),
            new Finding(3, "SC001", "unreachable: command 2 always ends the cycle or jumps away"));
    }

    [Fact]
    public void AddressedTerminatorDoesNotMakeFollowingCommandUnreachable()
    {
        AssertFindings("1d;p");
    }

    [Fact]
    public void NegatedAddressTerminatorDoesNotMakeFollowingCommandUnreachable()
    {
        AssertFindings("1!d;p");
    }

    [Fact]
    public void ClosingBlockRestoresReachableState()
    {
        AssertFindings(
            "{d;p};p",
            new Finding(3, "SC001", "unreachable: command 2 always ends the cycle or jumps away"));
    }

    [Fact]
    public void NestedBlocksRestoreTheirIncomingUnreachableState()
    {
        AssertFindings(
            "d;{q;{p};p};p",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"),
            new Finding(3, "SC001", "unreachable: command 1 always ends the cycle or jumps away"),
            new Finding(4, "SC001", "unreachable: command 3 always ends the cycle or jumps away"),
            new Finding(5, "SC001", "unreachable: command 3 always ends the cycle or jumps away"),
            new Finding(6, "SC001", "unreachable: command 3 always ends the cycle or jumps away"),
            new Finding(7, "SC001", "unreachable: command 1 always ends the cycle or jumps away"));
    }

    [Fact]
    public void LabelsResetReachabilityInsideBlocksWithoutChangingOuterState()
    {
        AssertFindings(
            "d;{:x;p};p;b x",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"),
            new Finding(5, "SC001", "unreachable: command 1 always ends the cycle or jumps away"),
            new Finding(6, "SC001", "unreachable: command 1 always ends the cycle or jumps away"));
    }

    [Fact]
    public void ReportsLabelsThatAreNeverUsed()
    {
        AssertFindings(":x", new Finding(1, "SC002", "label x is never used"));
    }

    [Fact]
    public void AcceptsBackwardBranchUseOfLabel()
    {
        AssertFindings(":x;b x");
    }

    [Fact]
    public void AcceptsForwardBranchUseAndLabelReachabilityReset()
    {
        AssertFindings("b x;:x");
    }

    [Fact]
    public void ReportsConditionalBranchWithoutPriorSubstitution()
    {
        AssertFindings("t x;:x", new Finding(1, "SC003", "t has no s command before it"));
    }

    [Fact]
    public void ReportsInverseConditionalBranchWithoutPriorSubstitution()
    {
        AssertFindings("T x;:x", new Finding(1, "SC003", "T has no s command before it"));
    }

    [Fact]
    public void TracksPriorSubstitutionsAcrossLaterBranches()
    {
        AssertFindings(
            "t;T;s/a/b/",
            new Finding(1, "SC003", "t has no s command before it"),
            new Finding(2, "SC003", "T has no s command before it"));
    }

    [Fact]
    public void TracksPriorSubstitutionAcrossBlocks()
    {
        AssertFindings("{s/a/b/};t");
    }

    [Fact]
    public void AcceptsSubstitutionBeforeBothConditionalBranches()
    {
        AssertFindings("s/a/b/;t;T");
    }

    [Fact]
    public void UnreachableSubstitutionStillCountsAsPriorInScriptOrder()
    {
        AssertFindings(
            "d;s/a/b/;:x;t x",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"));
    }

    [Fact]
    public void ReportsNegatedDescendingLineRange()
    {
        AssertFindings(
            "3,1!p",
            new Finding(1, "SC004", "range 3,1 ends before it starts, so only line 3 matches"));
    }

    [Fact]
    public void DoesNotReportAscendingEqualOrNonNumericRanges()
    {
        AssertFindings("1,1p;1,3p;/x/,1p;3,+0p");
    }

    [Fact]
    public void ReportsAllFindingsFromTheSuppliedUnreachableExample()
    {
        AssertFindings(
            "d;p;:x;s/a/b/",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"),
            new Finding(3, "SC002", "label x is never used"));
    }

    [Fact]
    public void ReportsTheSuppliedMixedFindingsInCommandOrder()
    {
        AssertFindings(
            "3,1p;t;:a;ba;5,6d;:u",
            new Finding(1, "SC004", "range 3,1 ends before it starts, so only line 3 matches"),
            new Finding(2, "SC003", "t has no s command before it"),
            new Finding(5, "SC001", "unreachable: command 4 always ends the cycle or jumps away"),
            new Finding(6, "SC002", "label u is never used"));
    }

    [Fact]
    public void ReturnsNoFindingsForTheSuppliedCleanExample()
    {
        const string script = @":a;N;$!ba;s/\n/,/g";
        IReadOnlyList<Cmd> commands = ScriptParser.Parse(script);

        Assert.Empty(Check.Run(commands));
        Assert.Equal(4, commands.Count);
    }

    [Fact]
    public void MeaningForPrintCommandMatchesThePublicContract()
    {
        Assert.Equal("print the pattern space", Explain.Meaning(ScriptParser.Parse("p")[0]));
    }

    [Fact]
    public void OrdersFindingsForOneCommandByOrdinalCode()
    {
        AssertFindings(
            "d;3,1t",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"),
            new Finding(2, "SC003", "t has no s command before it"),
            new Finding(2, "SC004", "range 3,1 ends before it starts, so only line 3 matches"));
    }

    [Fact]
    public void EmptyCommandListReturnsAnEmptyFindingList()
    {
        List<Finding> findings = Check.Run(ScriptParser.Parse(string.Empty));

        Assert.Empty(findings);
    }

    [Fact]
    public void RepeatedCallsAreDeterministicAndDoNotChangeCommands()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse("d;3,1t");
        Cmd[] originalCommands = commands.ToArray();
        string[] originalTexts = commands.Select(ScriptText.Of).ToArray();

        List<Finding> first = Check.Run(commands);
        List<Finding> second = Check.Run(commands);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.Equal(originalCommands, commands);
        Assert.Equal(originalTexts, commands.Select(ScriptText.Of));
    }

    private static void AssertFindings(string script, params Finding[] expected)
    {
        Assert.Equal(expected, Check.Run(ScriptParser.Parse(script)));
    }
}
