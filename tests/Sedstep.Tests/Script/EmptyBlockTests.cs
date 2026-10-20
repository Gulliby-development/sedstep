namespace Sedstep.Tests;

public class EmptyBlockTests
{
    [Fact]
    public void ReportsEmptyBlock()
    {
        AssertFindings("{}", new Finding(1, "SC005", "block is empty"));
    }

    [Fact]
    public void ReportsEmptyBlockAfterEarlierCommand()
    {
        AssertFindings("p;/x/{};p", new Finding(2, "SC005", "block is empty"));
    }

    [Fact]
    public void ReportsOnlyInnermostEmptyBlock()
    {
        AssertFindings("{{}}", new Finding(2, "SC005", "block is empty"));
    }

    [Fact]
    public void DoesNotReportBlockWithCommands()
    {
        AssertFindings("1{p};2{:x;bx}");
    }

    [Fact]
    public void ReportsEmptyBlockAlongsideOtherFindingsInCodeOrder()
    {
        AssertFindings(
            "d;{}",
            new Finding(2, "SC001", "unreachable: command 1 always ends the cycle or jumps away"),
            new Finding(2, "SC005", "block is empty"));
    }

    private static void AssertFindings(string script, params Finding[] expected)
    {
        Assert.Equal(expected, Check.Run(ScriptParser.Parse(script)));
    }
}
