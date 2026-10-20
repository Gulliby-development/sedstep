namespace Sedstep.Tests;

public class OptionsIoTests
{
    [Fact]
    public void FailurePreservesItsMessageAndIsSealedException()
    {
        Failure failure = new("example");

        Assert.Equal("example", failure.Message);
        Assert.IsAssignableFrom<Exception>(failure);
        Assert.True(typeof(Failure).IsSealed);
    }

    [Fact]
    public void OptionsParseEmptyArguments()
    {
        Parsed parsed = Options.Parse(Array.Empty<string>(), true);

        Assert.False(parsed.Quiet);
        Assert.Empty(parsed.Args);
    }

    [Fact]
    public void OptionsParseRepeatedQuietFlagsAmongArguments()
    {
        Parsed parsed = Options.Parse(new[] { "p", "-n", "-", "-n" }, true);

        Assert.True(parsed.Quiet);
        Assert.Equal(new[] { "p", "-" }, parsed.Args);
    }

    [Fact]
    public void OptionsTerminatorKeepsAllLaterArgumentsLiterally()
    {
        Parsed parsed = Options.Parse(new[] { "--", "-n", "--", "-x" }, true);

        Assert.False(parsed.Quiet);
        Assert.Equal(new[] { "-n", "--", "-x" }, parsed.Args);
    }

    [Fact]
    public void OptionsKeepOrdinaryArgumentsInOrderWhenQuietIsNotAllowed()
    {
        Parsed parsed = Options.Parse(new[] { "a", "", "-", "b" }, false);

        Assert.False(parsed.Quiet);
        Assert.Equal(new[] { "a", "", "-", "b" }, parsed.Args);
    }

    [Fact]
    public void OptionsAllowTerminatorByItself()
    {
        Parsed parsed = Options.Parse(new[] { "--" }, false);

        Assert.False(parsed.Quiet);
        Assert.Empty(parsed.Args);
    }

    [Fact]
    public void OptionsRejectQuietWhenItIsNotAllowed()
    {
        Failure failure = Assert.Throws<Failure>(() => Options.Parse(new[] { "-n" }, false));

        Assert.Equal("unknown option \"-n\"", failure.Message);
    }

    [Fact]
    public void OptionsReportTheFirstUnknownOption()
    {
        Failure failure = Assert.Throws<Failure>(() => Options.Parse(new[] { "p", "-x", "-y" }, true));

        Assert.Equal("unknown option \"-x\"", failure.Message);
    }

    [Fact]
    public void OptionsRenderNonAsciiInUnknownOptionDiagnostic()
    {
        Failure failure = Assert.Throws<Failure>(() => Options.Parse(new[] { "-😀" }, true));

        Assert.Equal("unknown option \"-?\"", failure.Message);
    }

    [Fact]
    public void InputReturnsLiteralArgumentWithoutReadingStdin()
    {
        FakeIo io = new(new byte[] { 0xFF });
        const string argument = "\uFEFFx\r\n";

        string result = Input.Arg(io, argument);

        Assert.Equal(argument, result);
        Assert.Equal(0, io.ReadCount);
        AssertNoWrites(io);
    }

    [Fact]
    public void InputReadsEmptyStdinAsEmptyText()
    {
        FakeIo io = new(Array.Empty<byte>());

        string result = Input.Arg(io, "-");

        Assert.Equal("", result);
        Assert.Equal(1, io.ReadCount);
        AssertNoWrites(io);
    }

    [Fact]
    public void InputDecodesStrictUtf8AndPreservesWhitespace()
    {
        FakeIo io = new(new byte[] { 0x61, 0xC3, 0xA9, 0xF0, 0x9F, 0x98, 0x80, 0x0D, 0x0A });

        string result = Input.Arg(io, "-");

        Assert.Equal("aé😀\r\n", result);
        Assert.Equal(1, io.ReadCount);
        AssertNoWrites(io);
    }

    [Fact]
    public void InputRemovesAStreamContainingOnlyTheLeadingBom()
    {
        FakeIo io = new(new byte[] { 0xEF, 0xBB, 0xBF });

        string result = Input.Arg(io, "-");

        Assert.Equal("", result);
        Assert.Equal(1, io.ReadCount);
        AssertNoWrites(io);
    }

    [Fact]
    public void InputRemovesExactlyOneLeadingBom()
    {
        FakeIo io = new(new byte[] { 0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, 0x61 });

        string result = Input.Arg(io, "-");

        Assert.Equal("\uFEFFa", result);
        Assert.Equal(1, io.ReadCount);
        AssertNoWrites(io);
    }

    [Fact]
    public void InputPreservesAnInteriorBom()
    {
        FakeIo io = new(new byte[] { 0x61, 0xEF, 0xBB, 0xBF });

        string result = Input.Arg(io, "-");

        Assert.Equal("a\uFEFF", result);
        Assert.Equal(1, io.ReadCount);
        AssertNoWrites(io);
    }

    [Fact]
    public void InputRejectsAnInvalidUtf8Byte()
    {
        AssertInvalidUtf8(new byte[] { 0x61, 0xFF, 0x0A });
    }

    [Fact]
    public void InputRejectsAnInvalidUtf8Continuation()
    {
        AssertInvalidUtf8(new byte[] { 0xC3, 0x28 });
    }

    [Fact]
    public void InputRejectsTruncatedUtf8()
    {
        AssertInvalidUtf8(new byte[] { 0xE2, 0x82 });
    }

    [Fact]
    public void InputRejectsOverlongUtf8()
    {
        AssertInvalidUtf8(new byte[] { 0xC0, 0xAF });
    }

    [Fact]
    public void InputRejectsUtf8EncodedSurrogate()
    {
        AssertInvalidUtf8(new byte[] { 0xED, 0xA0, 0x80 });
    }

    [Fact]
    public void LinesOfEmptyTextHasNoLinesAndFinalEol()
    {
        Split split = Lines.Of("");

        Assert.Empty(split.Lines);
        Assert.True(split.FinalEol);
    }

    [Fact]
    public void LinesOfUnterminatedTextHasOneLine()
    {
        Split split = Lines.Of("a");

        Assert.Equal(new[] { "a" }, split.Lines);
        Assert.False(split.FinalEol);
    }

    [Fact]
    public void LinesOfTerminatedTextHasOneLine()
    {
        Split split = Lines.Of("a\n");

        Assert.Equal(new[] { "a" }, split.Lines);
        Assert.True(split.FinalEol);
    }

    [Fact]
    public void LinesOfDoubleFinalNewlineKeepsTheEmptyLine()
    {
        Split split = Lines.Of("a\n\n");

        Assert.Equal(new[] { "a", "" }, split.Lines);
        Assert.True(split.FinalEol);
    }

    [Fact]
    public void LinesOfSingleNewlineContainsOneEmptyLine()
    {
        Split split = Lines.Of("\n");

        Assert.Equal(new[] { "" }, split.Lines);
        Assert.True(split.FinalEol);
    }

    [Fact]
    public void LinesOfPreservesLeadingAndInteriorEmptyLines()
    {
        Split split = Lines.Of("\na\n\nb");

        Assert.Equal(new[] { "", "a", "", "b" }, split.Lines);
        Assert.False(split.FinalEol);
    }

    [Fact]
    public void LinesOfPreservesCarriageReturns()
    {
        Split split = Lines.Of("a\r\nb\r");

        Assert.Equal(new[] { "a\r", "b\r" }, split.Lines);
        Assert.False(split.FinalEol);
    }

    [Fact]
    public void LinesOfPreservesSupplementaryCharacters()
    {
        Split split = Lines.Of("😀\nx");

        Assert.Equal(new[] { "😀", "x" }, split.Lines);
        Assert.False(split.FinalEol);
    }

    private static void AssertInvalidUtf8(byte[] bytes)
    {
        FakeIo io = new(bytes);

        Failure failure = Assert.Throws<Failure>(() => Input.Arg(io, "-"));

        Assert.Equal("<stdin>: input is not valid UTF-8", failure.Message);
        Assert.Equal(1, io.ReadCount);
        AssertNoWrites(io);
    }

    private static void AssertNoWrites(FakeIo io)
    {
        Assert.Empty(io.Outputs);
        Assert.Empty(io.Errors);
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
