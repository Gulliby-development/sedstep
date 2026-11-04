namespace Sedstep.Tests;

public class NextLabelTests
{
    private const string Message = "jump to label a does nothing: the label follows it";

    [Fact]
    public void ReportsBranchToTheNextLabel()
    {
        AssertFindings("ba;:a", new Finding(1, "SC006", Message));
    }

    [Fact]
    public void ReportsConditionalBranchesToTheNextLabel()
    {
        AssertFindings("s/x/y/;ta;:a", new Finding(2, "SC006", Message));
        AssertFindings("s/x/y/;Ta;:a", new Finding(2, "SC006", Message));
    }

    [Fact]
    public void ReportsBranchWithAnAddress()
    {
        AssertFindings("2,3ba;:a", new Finding(1, "SC006", Message));
    }

    [Fact]
    public void DoesNotReportWhenAnotherCommandComesBetween()
    {
        AssertFindings("1ba;p;:a");
    }

    [Fact]
    public void DoesNotReportWhenTheBlockEndsBetween()
    {
        AssertFindings("1{ba};:a");
    }

    [Fact]
    public void DoesNotReportBranchToTheEndOfTheScript()
    {
        AssertFindings("p;b");
    }

    [Fact]
    public void DoesNotReportBackwardBranch()
    {
        AssertFindings(":a;p;ba");
    }

    [Fact]
    public void ReportsOnlyTheBranchThatFollowsItsLabelDirectly()
    {
        AssertFindings(
            ":a;1ba;bb;:b",
            new Finding(3, "SC006", "jump to label b does nothing: the label follows it"));
    }

    [Fact]
    public void ReportsAfterTheOtherCodesOfTheSameCommand()
    {
        AssertFindings(
            "ta;:a",
            new Finding(1, "SC003", "t has no s command before it"),
            new Finding(1, "SC006", Message));
    }

    [Fact]
    public void CheckCommandPrintsTheFinding()
    {
        RecordingIo io = new();

        int exitCode = Cli.Run(["check", "s/a/b/;ba;:a"], io);

        Assert.Equal(1, exitCode);
        Assert.Equal(
            ["command 2: SC006 jump to label a does nothing: the label follows it\n1 finding\n"],
            io.Outputs);
        Assert.Empty(io.Errors);
    }

    private static void AssertFindings(string script, params Finding[] expected)
    {
        Assert.Equal(expected, Check.Run(ScriptParser.Parse(script)));
    }

    private sealed class RecordingIo : IIo
    {
        public List<string> Outputs { get; } = [];

        public List<string> Errors { get; } = [];

        public byte[] ReadStdin()
        {
            throw new InvalidOperationException("unexpected read");
        }

        public void Out(string text)
        {
            Outputs.Add(text);
        }

        public void Err(string text)
        {
            Errors.Add(text);
        }
    }
}
