namespace Sedstep.Tests;

public class DocsTests
{
    [Fact]
    public void ReadmeContainsRequiredHeadings()
    {
        string readme = ReadReadme();

        Assert.Contains("## Install\n", readme);
        Assert.Contains("## Usage\n", readme);
        Assert.Contains("## Commands\n", readme);
        Assert.Contains("## Exit codes\n", readme);
        Assert.Contains("## Development\n", readme);
    }

    [Fact]
    public void ReadmeContainsExactUsageAndRunExample()
    {
        string readme = ReadReadme();

        const string usageBlock =
            "```text\n" +
            "usage: sedstep run [-n] SCRIPT TEXT\n" +
            "       sedstep trace [-n] SCRIPT TEXT\n" +
            "       sedstep explain SCRIPT\n" +
            "       sedstep check SCRIPT\n" +
            "```\n";
        const string runExample =
            "`printf 'one\\ntwo\\nthree\\nfour\\n' | sedstep run -n '2,3p' -` prints:\n\n" +
            "```text\n" +
            "two\n" +
            "three\n" +
            "```\n";

        Assert.Contains(usageBlock, readme);
        Assert.Contains(runExample, readme);
    }

    [Fact]
    public void ReadmeContainsExactReversalTraceExample()
    {
        string readme = ReadReadme();

        const string traceExample =
            "`printf 'one\\ntwo\\nthree\\n' | sedstep trace '1!G;h;$!d' -` prints:\n\n" +
            "```text\n" +
            "line  cmd  command  pattern            hold               out\n" +
            "1     2    [h]      [one]              [one]              -\n" +
            "1     3    [$!d]    []                 [one]              -\n" +
            "2     1    [1!G]    [two\\none]         [one]              -\n" +
            "2     2    [h]      [two\\none]         [two\\none]         -\n" +
            "2     3    [$!d]    []                 [two\\none]         -\n" +
            "3     1    [1!G]    [three\\ntwo\\none]  [two\\none]         -\n" +
            "3     2    [h]      [three\\ntwo\\none]  [three\\ntwo\\none]  -\n" +
            "3     -    (end)    [three\\ntwo\\none]  [three\\ntwo\\none]  [three\\ntwo\\none\\n]\n" +
            "steps       7\n" +
            "cycles      3\n" +
            "```\n";

        Assert.Contains(traceExample, readme);
    }

    private static string ReadReadme()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string readmePath = Path.Combine(directory.FullName, "README.md");
            if (File.Exists(readmePath))
            {
                return File.ReadAllText(readmePath).Replace("\r\n", "\n", StringComparison.Ordinal);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"README.md was not found walking upward from '{AppContext.BaseDirectory}'.");
        return string.Empty;
    }
}
