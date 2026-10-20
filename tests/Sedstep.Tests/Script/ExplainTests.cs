namespace Sedstep.Tests;

public class ExplainTests
{
    [Theory]
    [InlineData("2p", "on line 2, print the pattern space")]
    [InlineData("$p", "on the last line, print the pattern space")]
    [InlineData("/x/p", "on lines that match [x], print the pattern space")]
    [InlineData("2!p", "except line 2, print the pattern space")]
    [InlineData("$!p", "except the last line, print the pattern space")]
    [InlineData("/x/!p", "except lines that match [x], print the pattern space")]
    [InlineData("2,4p", "in the range from line 2 to line 4, print the pattern space")]
    [InlineData("0,/x/p", "in the range from the start of the input to the next line that matches [x], print the pattern space")]
    [InlineData("$,/x/p", "in the range from the last line to the next line that matches [x], print the pattern space")]
    [InlineData("/x/,$p", "in the range from a line that matches [x] to the last line, print the pattern space")]
    [InlineData("/x/,+0p", "in the range from a line that matches [x] to the same line, print the pattern space")]
    [InlineData("/x/,+1p", "in the range from a line that matches [x] to the next line, print the pattern space")]
    [InlineData("/x/,+3p", "in the range from a line that matches [x] to the line 3 lines later, print the pattern space")]
    [InlineData("2,4!p", "outside the range from line 2 to line 4, print the pattern space")]
    public void MeaningDescribesSingleAddressesRangesAndNegation(string script, string expected)
    {
        Cmd command = Assert.Single(ScriptParser.Parse(script));

        Assert.Equal(expected, Explain.Meaning(command));
    }
    [Theory]
    [InlineData("s/a/b/", "replace the first match of [a] with [b]")]
    [InlineData("s/a/b/g", "replace every match of [a] with [b]")]
    [InlineData("s/a/b/2", "replace match number 2 of [a] with [b]")]
    [InlineData("s/a/b/2gp", "replace match number 2 and every later match of [a] with [b] and print the pattern space if a replacement was made")]
    [InlineData("s#a/b#c/d#p", "replace the first match of [a/b] with [c/d] and print the pattern space if a replacement was made")]
    [InlineData("s/😀/é/", "replace the first match of [?] with [?]")]
    [InlineData("i 😀", "print the text [?] now")]
    public void MeaningDescribesSubstitutionsAndSupplementaryText(string script, string expected)
    {
        Cmd command = Assert.Single(ScriptParser.Parse(script));

        Assert.Equal(expected, Explain.Meaning(command));
    }

    [Fact]
    public void MeaningDescribesBlockAndNamedBranchActions()
    {
        IReadOnlyList<Cmd> blockCommands = ScriptParser.Parse("{p}");
        IReadOnlyList<Cmd> namedBranchCommands = ScriptParser.Parse(":a;b a;t a;T a");

        Assert.Equal(
            new[]
            {
                "run the commands up to the matching }",
                "print the pattern space",
            },
            blockCommands.Take(2).Select(Explain.Meaning));
        Assert.Equal(
            new[]
            {
                "define the label a",
                "jump to the label a",
                "jump to the label a if a substitution succeeded since the last line was read or the last t",
                "jump to the label a if no substitution succeeded since the last line was read or the last T",
            },
            namedBranchCommands.Select(Explain.Meaning));
    }

    [Fact]
    public void MeaningDescribesBareBranchesAndEverySimpleCommand()
    {
        IReadOnlyList<Cmd> branchCommands = ScriptParser.Parse("b;t;T");
        IReadOnlyList<Cmd> simpleCommands = ScriptParser.Parse("d;D;p;P;n;N;g;G;h;H;x;=;q;Q");

        Assert.Equal(
            new[]
            {
                "jump to the end of the script",
                "jump to the end of the script if a substitution succeeded since the last line was read or the last t",
                "jump to the end of the script if no substitution succeeded since the last line was read or the last T",
            },
            branchCommands.Select(Explain.Meaning));
        Assert.Equal(
            new[]
            {
                "delete the pattern space and start the next cycle",
                "delete up to the first newline of the pattern space and restart the cycle without reading a line",
                "print the pattern space",
                "print the pattern space up to its first newline",
                "print the pattern space (unless -n is given), then replace it with the next line; with no next line, end the script",
                "append a newline and the next line to the pattern space; with no next line, end the script",
                "replace the pattern space with the hold space",
                "append a newline and the hold space to the pattern space",
                "replace the hold space with the pattern space",
                "append a newline and the pattern space to the hold space",
                "swap the pattern space and the hold space",
                "print the line number",
                "print the pattern space (unless -n is given) and quit",
                "quit without printing",
            },
            simpleCommands.Select(Explain.Meaning));
    }

    [Theory]
    [InlineData("a bye", "queue the text [bye] to be printed at the end of the cycle")]
    [InlineData("i bye", "print the text [bye] now")]
    public void MeaningDescribesAppendAndInsertActions(string script, string expected)
    {
        Cmd command = Assert.Single(ScriptParser.Parse(script));

        Assert.Equal(expected, Explain.Meaning(command));
    }

    [Fact]
    public void MeaningDescribesTheSuppliedExample()
    {
        IReadOnlyList<Cmd> commands = ScriptParser.Parse(@":a;N;$!ba;s/\n/,/2gp;2,4!{p;D};/x/,+1i bye");

        Assert.Equal(
            new[]
            {
                "define the label a",
                "append a newline and the next line to the pattern space; with no next line, end the script",
                "except the last line, jump to the label a",
                "replace match number 2 and every later match of [\\n] with [,] and print the pattern space if a replacement was made",
                "outside the range from line 2 to line 4, run the commands up to the matching }",
                "print the pattern space",
                "delete up to the first newline of the pattern space and restart the cycle without reading a line",
                "in the range from a line that matches [x] to the next line, print the text [bye] now",
            },
            commands.Where(command => command.Letter != '}').Select(Explain.Meaning));
    }

    [Fact]
    public void MeaningIsRepeatableAndDoesNotChangeCommands()
    {
        Cmd command = Assert.Single(ScriptParser.Parse("p"));
        var before = (
            command.Letter,
            command.Number,
            command.First,
            command.Second,
            command.Negated,
            command.Label,
            command.Target,
            command.Jump,
            command.Regex,
            command.RegexSource,
            command.Repl,
            command.ReplSource,
            command.Global,
            command.Print,
            command.Count,
            command.Text);

        string first = Explain.Meaning(command);
        string second = Explain.Meaning(command);
        string third = Explain.Meaning(command);

        Assert.Equal("print the pattern space", first);
        Assert.Equal(first, second);
        Assert.Equal(first, third);
        Assert.Equal(before, (
            command.Letter,
            command.Number,
            command.First,
            command.Second,
            command.Negated,
            command.Label,
            command.Target,
            command.Jump,
            command.Regex,
            command.RegexSource,
            command.Repl,
            command.ReplSource,
            command.Global,
            command.Print,
            command.Count,
            command.Text));
    }
}
