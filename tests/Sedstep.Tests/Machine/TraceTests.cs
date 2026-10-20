namespace Sedstep.Tests;

public class TraceTests
{
    [Fact]
    public void ReversalExampleRecordsExactSnapshotsAndOriginalCommands()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse("1!G;h;$!d");

        Execution execution = Machine.Execute(
            commands,
            "one\ntwo\nthree\n",
            quiet: false,
            trace: true);

        Assert.Equal("three\ntwo\none\n", execution.Output);
        Assert.Equal(7, execution.Steps);
        Assert.Equal(3, execution.Cycles);
        Assert.Equal(8, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, commands[1], "", "one", "one");
        AssertRow(execution.Rows[1], 1, commands[2], "", "", "one");
        AssertRow(execution.Rows[2], 2, commands[0], "", "two\none", "one");
        AssertRow(execution.Rows[3], 2, commands[1], "", "two\none", "two\none");
        AssertRow(execution.Rows[4], 2, commands[2], "", "", "two\none");
        AssertRow(execution.Rows[5], 3, commands[0], "", "three\ntwo\none", "two\none");
        AssertRow(execution.Rows[6], 3, commands[1], "", "three\ntwo\none", "three\ntwo\none");
        AssertRow(execution.Rows[7], 3, null, "three\ntwo\none\n", "three\ntwo\none", "three\ntwo\none");
    }

    [Fact]
    public void TraceAttributesPendingLineFeedToTheWriteThatFlushesIt()
    {
        Execution execution = Execute("p;p", "x", quiet: true);

        Assert.Equal("x\nx", execution.Output);
        Assert.Equal(2, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(3, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "x", "x", "");
        AssertRow(execution.Rows[1], 1, executionCommand(execution, 2), "\nx", "x", "");
        AssertRow(execution.Rows[2], 1, null, "", "x", "");
    }

    [Fact]
    public void DeleteAttributesQueuedTextToTheDeletingCommand()
    {
        Execution execution = Execute("a tail\nd", "x\n", quiet: false);

        Assert.Equal("tail\n", execution.Output);
        Assert.Equal(2, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(2, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "", "x", "");
        AssertRow(execution.Rows[1], 1, executionCommand(execution, 2), "tail\n", "", "");
    }

    [Fact]
    public void QuitAttributesPatternAndQueuedTextToQuitCommand()
    {
        Execution execution = Execute("a tail\nq", "x", quiet: false);

        Assert.Equal("x\ntail\n", execution.Output);
        Assert.Equal(2, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(2, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "", "x", "");
        AssertRow(execution.Rows[1], 1, executionCommand(execution, 2), "x\ntail\n", "x", "");
    }

    [Fact]
    public void SilentQuitDoesNotFlushPendingLineFeedOrQueuedText()
    {
        Execution execution = Execute("p;a tail\nQ", "x", quiet: true);

        Assert.Equal("x", execution.Output);
        Assert.Equal(3, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(3, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "x", "x", "");
        AssertRow(execution.Rows[1], 1, executionCommand(execution, 2), "", "x", "");
        AssertRow(execution.Rows[2], 1, executionCommand(execution, 3), "", "x", "");
    }

    [Fact]
    public void BranchToScriptEndAttributesNormalCompletionToEndRow()
    {
        Execution execution = Execute("a tail\nb", "x\n", quiet: false);

        Assert.Equal("x\ntail\n", execution.Output);
        Assert.Equal(2, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(3, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "", "x", "");
        AssertRow(execution.Rows[1], 1, executionCommand(execution, 2), "", "x", "");
        AssertRow(execution.Rows[2], 1, null, "x\ntail\n", "x", "");
    }

    [Fact]
    public void SubstitutionPrintAndNormalEndHaveSeparateOutputAttribution()
    {
        Execution execution = Execute("s/x/y/p", "x\n", quiet: false);

        Assert.Equal("y\ny\n", execution.Output);
        Assert.Equal(1, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(2, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "y\n", "y", "");
        AssertRow(execution.Rows[1], 1, null, "y\n", "y", "");
    }

    [Fact]
    public void SkippedBlockAndLabelProduceOnlyAnEmptyNormalEndRow()
    {
        Execution execution = Execute("2{p};:a", "x\n", quiet: true);

        Assert.Empty(execution.Output);
        Assert.Equal(0, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Single(execution.Rows);
        AssertRow(execution.Rows[0], 1, null, "", "x", "");
    }

    [Fact]
    public void FailedNextCommandPrintsAndHasNoNormalEndRow()
    {
        Execution execution = Execute("n", "x", quiet: false);

        Assert.Equal("x", execution.Output);
        Assert.Equal(1, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Single(execution.Rows);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "x", "x", "");
    }

    [Fact]
    public void FailedAppendNextCommandPrintsAndHasNoNormalEndRow()
    {
        Execution execution = Execute("N", "x\n", quiet: false);

        Assert.Equal("x\n", execution.Output);
        Assert.Equal(1, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Single(execution.Rows);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "x\n", "x", "");
    }

    [Fact]
    public void SuccessfulNextCommandAttributesPrintAndQueuedTextToRead()
    {
        Execution execution = Execute("a tail\nn", "x\ny", quiet: false);

        Assert.Equal("x\ntail\ny", execution.Output);
        Assert.Equal(2, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(3, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "", "x", "");
        AssertRow(execution.Rows[1], 2, executionCommand(execution, 2), "x\ntail\n", "y", "");
        AssertRow(execution.Rows[2], 2, null, "y", "y", "");
    }

    [Fact]
    public void SuccessfulAppendNextAttributesQueueAndAddsNormalEndRow()
    {
        Execution execution = Execute("a tail\nN", "x\ny", quiet: true);

        Assert.Equal("tail\n", execution.Output);
        Assert.Equal(2, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(3, execution.Rows.Count);
        AssertRow(execution.Rows[0], 1, executionCommand(execution, 1), "", "x", "");
        AssertRow(execution.Rows[1], 2, executionCommand(execution, 2), "tail\n", "x\ny", "");
        AssertRow(execution.Rows[2], 2, null, "", "x\ny", "");
    }

    [Fact]
    public void DeleteFirstRestartsWithoutReadingAndDeleteSecondEndsCycle()
    {
        Execution execution = Execute("$!N;D", "a\nb\n", quiet: false);

        Assert.Empty(execution.Output);
        Assert.Equal(3, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(3, execution.Rows.Count);
        AssertRow(execution.Rows[0], 2, executionCommand(execution, 1), "", "a\nb", "");
        AssertRow(execution.Rows[1], 2, executionCommand(execution, 2), "", "b", "");
        AssertRow(execution.Rows[2], 2, executionCommand(execution, 2), "", "", "");
    }

    [Fact]
    public void NextReadsDoNotAddOuterCyclesAndFailedReadOwnsCompletion()
    {
        Execution execution = Execute("n", "a\nb\nc\n", quiet: false);

        Assert.Equal("a\nb\nc\n", execution.Output);
        Assert.Equal(2, execution.Steps);
        Assert.Equal(2, execution.Cycles);
        Assert.Equal(3, execution.Rows.Count);
        AssertRow(execution.Rows[0], 2, executionCommand(execution, 1), "a\n", "b", "");
        AssertRow(execution.Rows[1], 2, null, "b\n", "b", "");
        AssertRow(execution.Rows[2], 3, executionCommand(execution, 1), "c\n", "c", "");
    }

    [Fact]
    public void EmptyInputHasNoCyclesStepsOrTraceRows()
    {
        Execution execution = Execute("p", "", quiet: false);

        Assert.Empty(execution.Output);
        Assert.Equal(0, execution.Steps);
        Assert.Equal(0, execution.Cycles);
        Assert.Empty(execution.Rows);
    }

    [Fact]
    public void TwentyExecutedCommandsHaveTwentyOutputEventsAndAnEndRow()
    {
        Execution execution = Execute("=;=;=;=;=;=;=;=;=;=;=;=;=;=;=;=;=;=;=;=", "x\n", quiet: true);

        Assert.Equal("1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n1\n", execution.Output);
        Assert.Equal(20, execution.Steps);
        Assert.Equal(1, execution.Cycles);
        Assert.Equal(21, execution.Rows.Count);
        Assert.Null(execution.Rows[20].Command);
        Assert.Equal("", execution.Rows[20].Out);
    }

    private static Execution Execute(string script, string text, bool quiet)
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse(script);
        return Machine.Execute(commands, text, quiet, trace: true);
    }

    private static Cmd executionCommand(Execution execution, int number) =>
        execution.Rows.First(row => row.Command?.Number == number).Command!;

    private static void AssertRow(Row row, int line, Cmd? command, string expectedOut, string expectedPattern, string expectedHold)
    {
        Assert.Equal(line, row.Line);
        Assert.Same(command, row.Command);
        Assert.Equal(expectedOut, row.Out);
        Assert.Equal(expectedPattern, row.Pattern);
        Assert.Equal(expectedHold, row.Hold);
    }
}
