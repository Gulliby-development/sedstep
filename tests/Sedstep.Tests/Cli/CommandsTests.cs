namespace Sedstep.Tests;

public class CommandsTests
{
    [Fact]
    public void RunRejectsUnknownOptionBeforeArguments()
    {
        AssertValidationFailure(
            new[] { "run", "-x", "p", "a" },
            "sedstep: unknown option \"-x\"\n");
    }

    [Fact]
    public void RunRejectsUnknownOptionBeforeArity()
    {
        AssertValidationFailure(
            new[] { "run", "-x" },
            "sedstep: unknown option \"-x\"\n");
    }

    [Fact]
    public void RunRequiresScript()
    {
        AssertValidationFailure(
            new[] { "run" },
            "sedstep: missing script\n");
    }

    [Fact]
    public void TraceRequiresTextAfterScript()
    {
        AssertValidationFailure(
            new[] { "trace", "p" },
            "sedstep: missing text\n");
    }

    [Fact]
    public void ExplainRequiresScript()
    {
        AssertValidationFailure(
            new[] { "explain" },
            "sedstep: missing script\n");
    }

    [Fact]
    public void CheckRejectsTooManyArguments()
    {
        AssertValidationFailure(
            new[] { "check", "p", "p" },
            "sedstep: too many arguments\n");
    }

    [Fact]
    public void RunChecksArityBeforeDuplicateStdin()
    {
        AssertValidationFailure(
            new[] { "run", "-", "-", "x" },
            "sedstep: too many arguments\n");
    }

    [Fact]
    public void RunRejectsDuplicateStdinArguments()
    {
        AssertValidationFailure(
            new[] { "run", "-", "-" },
            "sedstep: only one argument can be -\n");
    }

    [Fact]
    public void ExplainRejectsQuietOption()
    {
        AssertValidationFailure(
            new[] { "explain", "-n", "p" },
            "sedstep: unknown option \"-n\"\n");
    }

    [Fact]
    public void CheckRejectsOptionAfterScript()
    {
        AssertValidationFailure(
            new[] { "check", "p", "-n" },
            "sedstep: unknown option \"-n\"\n");
    }

    [Fact]
    public void RunParsesScriptBeforeReadingTextFromStdin()
    {
        AssertCliFailure(
            new[] { "run", "k", "-" },
            new byte[] { 0x61, 0xFF, 0x0A },
            "sedstep: unknown command \"k\" at line 1 column 1\n",
            expectedReads: 0);
    }

    [Fact]
    public void RunRejectsInvalidUtf8InScriptStdin()
    {
        AssertCliFailure(
            new[] { "run", "-", "x" },
            new byte[] { 0xFF },
            "sedstep: <stdin>: input is not valid UTF-8\n",
            expectedReads: 1);
    }

    [Fact]
    public void RunRejectsInvalidUtf8InTextStdin()
    {
        AssertCliFailure(
            new[] { "run", "p", "-" },
            new byte[] { 0x61, 0xFF, 0x0A },
            "sedstep: <stdin>: input is not valid UTF-8\n",
            expectedReads: 1);
    }

    [Fact]
    public void RunRemovesLeadingBomFromTextStdin()
    {
        AssertCliSuccess(
            new[] { "run", "-n", "p", "-" },
            new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0x0A },
            "a\n",
            expectedReads: 1);
    }

    [Fact]
    public void ExplainReadsScriptFromStdin()
    {
        AssertCliSuccess(
            new[] { "explain", "-" },
            new byte[] { 0x70 },
            "n  command  meaning\n1  [p]      print the pattern space\ncommands    1\n",
            expectedReads: 1);
    }

    [Fact]
    public void CheckDoesNotReadStdinWhenScriptParsingFails()
    {
        AssertCliFailure(
            new[] { "check", "s/a/b/gg" },
            new byte[] { 0x70 },
            "sedstep: multiple g flags at line 1 column 8\n",
            expectedReads: 0);
    }

    [Fact]
    public void RunRendersRangeOutputInOneCall()
    {
        AssertCliSuccess(
            new[] { "run", "-n", "2,3p", "one\ntwo\nthree\nfour\n" },
            Array.Empty<byte>(),
            "two\nthree\n",
            expectedReads: 0);
    }

    [Fact]
    public void RunPreservesUnterminatedFinalLineOutput()
    {
        AssertCliSuccess(
            new[] { "run", "p", "a\nb" },
            Array.Empty<byte>(),
            "a\na\nb\nb",
            expectedReads: 0);
    }

    [Fact]
    public void RunAllowsRepeatedQuietFlags()
    {
        AssertCliSuccess(
            new[] { "run", "-n", "-n", "p", "x\n" },
            Array.Empty<byte>(),
            "x\n",
            expectedReads: 0);
    }

    [Fact]
    public void RunRecognizesOptionsAfterTheScript()
    {
        AssertCliSuccess(
            new[] { "run", "p", "-n", "x\n" },
            Array.Empty<byte>(),
            "x\n",
            expectedReads: 0);
    }

    [Fact]
    public void RunHonorsOptionTerminator()
    {
        AssertCliSuccess(
            new[] { "run", "-n", "p", "--", "-n" },
            Array.Empty<byte>(),
            "-n",
            expectedReads: 0);
    }

    [Fact]
    public void RunMakesOneEmptyOutputCallAfterDeletion()
    {
        AssertCliSuccess(
            new[] { "run", "d", "x\n" },
            Array.Empty<byte>(),
            "",
            expectedReads: 0);
    }

    [Fact]
    public void RunMakesOneEmptyOutputCallForEmptyInput()
    {
        AssertCliSuccess(
            new[] { "run", "p", "" },
            Array.Empty<byte>(),
            "",
            expectedReads: 0);
    }

    [Fact]
    public void RunProcessesSupplementaryUnicodeCharacters()
    {
        AssertCliSuccess(
            new[] { "run", "s/./Y/", "😀x\n" },
            Array.Empty<byte>(),
            "Yx\n",
            expectedReads: 0);
    }

    [Fact]
    public void RunWritesAppendQueueAfterThePatternSpace()
    {
        AssertCliSuccess(
            new[] { "run", "s/x/[&]/;a end", "x\n" },
            Array.Empty<byte>(),
            "[x]\nend\n",
            expectedReads: 0);
    }

    [Fact]
    public void RunBuffersOutputWhenCommandLimitFails()
    {
        AssertCliFailure(
            new[] { "run", ":a;p;ba", "x" },
            Array.Empty<byte>(),
            "sedstep: the command limit of 100000 is exceeded\n",
            expectedReads: 0);
    }

    [Fact]
    public void RunBuffersOutputWhenPatternSpaceLimitFails()
    {
        AssertCliFailure(
            new[] { "run", ":a;H;G;ba", "x" },
            Array.Empty<byte>(),
            "sedstep: the pattern space is larger than 10000 characters\n",
            expectedReads: 0);
    }

    [Fact]
    public void TraceRendersNormalEndRowsAndCountsExactly()
    {
        AssertCliSuccess(
            new[] { "trace", "p", "x\n" },
            Array.Empty<byte>(),
            "line  cmd  command  pattern  hold  out\n" +
            "1     1    [p]      [x]      []    [x\\n]\n" +
            "1     -    (end)    [x]      []    [x\\n]\n" +
            "steps       1\n" +
            "cycles      1\n",
            expectedReads: 0);
    }

    [Fact]
    public void TraceRendersDeletionWithoutANormalEndRow()
    {
        AssertCliSuccess(
            new[] { "trace", "-n", "d", "x\n" },
            Array.Empty<byte>(),
            "line  cmd  command  pattern  hold  out\n" +
            "1     1    [d]      []       []    -\n" +
            "steps       1\n" +
            "cycles      1\n",
            expectedReads: 0);
    }

    [Fact]
    public void TraceRendersEmptyInputWithHeadersAndZeroCounts()
    {
        AssertCliSuccess(
            new[] { "trace", "p", "" },
            Array.Empty<byte>(),
            "line  cmd  command  pattern  hold  out\n" +
            "steps       0\n" +
            "cycles      0\n",
            expectedReads: 0);
    }

    [Fact]
    public void TraceRendersTheSuppliedReversalTableExactly()
    {
        AssertCliSuccess(
            new[] { "trace", "1!G;h;$!d", "one\ntwo\nthree\n" },
            Array.Empty<byte>(),
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
            "cycles      3\n",
            expectedReads: 0);
    }

    [Fact]
    public void ExplainRendersSingleCommandTableExactly()
    {
        AssertCliOutput(
            new[] { "explain", "p" },
            0,
            "n  command  meaning\n" +
            "1  [p]      print the pattern space\n" +
            "commands    1\n");
    }

    [Fact]
    public void ExplainRendersEmptyScriptAndZeroCommands()
    {
        AssertCliOutput(
            new[] { "explain", "" },
            0,
            "n  command  meaning\n" +
            "commands    0\n");
    }

    [Fact]
    public void ExplainExcludesClosingBraceFromCommandCount()
    {
        AssertCliOutput(
            new[] { "explain", "{p}" },
            0,
            "n  command  meaning\n" +
            "1  [{]      run the commands up to the matching }\n" +
            "2  [p]      print the pattern space\n" +
            "commands    2\n");
    }

    [Fact]
    public void ExplainRendersTheSuppliedTableAndCanonicalTextExactly()
    {
        // Supplied invocation: explain ':a;N;$!ba;s/\n/,/2gp;2,4!{p;D};/x/,+1i bye'
        // Escaped spelling: explain ':a;N;$!ba;s/\\n/,/2gp;2,4!{p;D};/x/,+1i bye'
        AssertCliOutput(
            new[] { "explain", ":a;N;$!ba;s/\\n/,/2gp;2,4!{p;D};/x/,+1i bye" },
            0,
            "n  command        meaning\n" +
            "1  [:a]           define the label a\n" +
            "2  [N]            append a newline and the next line to the pattern space; with no next line, end the script\n" +
            "3  [$!b a]        except the last line, jump to the label a\n" +
            "4  [s/\\n/,/2gp]   replace match number 2 and every later match of [\\n] with [,] and print the pattern space if a replacement was made\n" +
            "5  [2,4!{]        outside the range from line 2 to line 4, run the commands up to the matching }\n" +
            "6  [p]            print the pattern space\n" +
            "7  [D]            delete up to the first newline of the pattern space and restart the cycle without reading a line\n" +
            "8  [/x/,+1i bye]  in the range from a line that matches [x] to the next line, print the text [bye] now\n" +
            "commands    8\n");
    }

    [Fact]
    public void CheckRendersEmptyScriptAsZeroCommands()
    {
        AssertCliOutput(
            new[] { "check", "" },
            0,
            "ok, 0 commands\n");
    }

    [Fact]
    public void CheckUsesSingularCommandGrammar()
    {
        AssertCliOutput(
            new[] { "check", "p" },
            0,
            "ok, 1 command\n");
    }

    [Fact]
    public void CheckExcludesClosingBraceFromCommandCount()
    {
        AssertCliOutput(
            new[] { "check", "{p}" },
            0,
            "ok, 2 commands\n");
    }

    [Fact]
    public void CheckUsesSingularFindingGrammarAndExitOne()
    {
        AssertCliOutput(
            new[] { "check", "t" },
            1,
            "command 1: SC003 t has no s command before it\n" +
            "1 finding\n");
    }

    [Fact]
    public void CheckRendersPluralFindingsExactly()
    {
        AssertCliOutput(
            ["check", "d;p;:x;s/a/b/"],
            1,
            "command 2: SC001 unreachable: command 1 always ends the cycle or jumps away\n" +
            "command 3: SC002 label x is never used\n" +
            "2 findings\n");
    }

    [Fact]
    public void CheckOrdersFindingsByCodeForTheSameCommand()
    {
        AssertCliOutput(
            new[] { "check", "d;3,1p" },
            1,
            "command 2: SC001 unreachable: command 1 always ends the cycle or jumps away\n" +
            "command 2: SC004 range 3,1 ends before it starts, so only line 3 matches\n" +
            "2 findings\n");
    }

    [Fact]
    public void CheckRendersTheSuppliedFourFindingsExactly()
    {
        AssertCliOutput(
            new[] { "check", "3,1p;t;:a;ba;5,6d;:u" },
            1,
            "command 1: SC004 range 3,1 ends before it starts, so only line 3 matches\n" +
            "command 2: SC003 t has no s command before it\n" +
            "command 5: SC001 unreachable: command 4 always ends the cycle or jumps away\n" +
            "command 6: SC002 label u is never used\n" +
            "4 findings\n");
    }

    [Fact]
    public void CheckRendersCleanScriptAsPluralCommandCount()
    {
        AssertCliOutput(
            new[] { "check", ":a;N;$!ba;s/\\n/,/g" },
            0,
            "ok, 4 commands\n");
    }

    private static void AssertCliOutput(string[] argv, int expectedExitCode, string expectedOutput)
    {
        FakeIo io = new(Array.Empty<byte>());

        int exitCode = Cli.Run(argv, io);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(new[] { expectedOutput }, io.Outputs);
        Assert.Empty(io.Errors);
        Assert.Equal(0, io.ReadCount);
    }

    private static void AssertValidationFailure(string[] argv, string expectedError)
    {
        AssertCliFailure(argv, Array.Empty<byte>(), expectedError, expectedReads: 0);
    }

    private static void AssertCliFailure(string[] argv, byte[] stdin, string expectedError, int expectedReads)
    {
        FakeIo io = new(stdin);

        int exitCode = Cli.Run(argv, io);

        Assert.Equal(2, exitCode);
        Assert.Empty(io.Outputs);
        Assert.Equal(new[] { expectedError }, io.Errors);
        Assert.Equal(expectedReads, io.ReadCount);
    }

    private static void AssertCliSuccess(string[] argv, byte[] stdin, string expectedOutput, int expectedReads)
    {
        FakeIo io = new(stdin);

        int exitCode = Cli.Run(argv, io);

        Assert.Equal(0, exitCode);
        Assert.Equal(new[] { expectedOutput }, io.Outputs);
        Assert.Empty(io.Errors);
        Assert.Equal(expectedReads, io.ReadCount);
    }

    private sealed class FakeIo : IIo
    {
        private readonly byte[] stdin;

        public FakeIo(byte[] stdin)
        {
            this.stdin = stdin;
        }

        public int ReadCount { get; private set; }

        public List<string> Outputs { get; } = [];

        public List<string> Errors { get; } = [];

        public byte[] ReadStdin()
        {
            ReadCount++;
            return stdin;
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
