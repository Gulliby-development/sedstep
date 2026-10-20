namespace Sedstep;

public enum AddrKind
{
    Line,
    Last,
    Regex,
    Plus,
}

public sealed record Addr(AddrKind Kind, int A, string Source, Pattern? Pattern);

public sealed record ReplPart(int Group, string Text);

public sealed class Cmd
{
    public Cmd(
        char letter,
        int number,
        Addr? first = null,
        Addr? second = null,
        bool negated = false,
        string label = "",
        int target = -1,
        int jump = -1,
        Pattern? regex = null,
        string regexSource = "",
        IReadOnlyList<ReplPart>? repl = null,
        string replSource = "",
        bool global = false,
        bool print = false,
        int count = 0,
        string text = "")
    {
        Letter = letter;
        Number = number;
        First = first;
        Second = second;
        Negated = negated;
        Label = label;
        Target = target;
        Jump = jump;
        Regex = regex;
        RegexSource = regexSource;
        Repl = Array.AsReadOnly(repl?.ToArray() ?? Array.Empty<ReplPart>());
        ReplSource = replSource;
        Global = global;
        Print = print;
        Count = count;
        Text = text;
    }

    public char Letter { get; }

    public int Number { get; }

    public Addr? First { get; }

    public Addr? Second { get; }

    public bool Negated { get; }

    public string Label { get; }

    public int Target { get; }

    public int Jump { get; }

    public Pattern? Regex { get; }

    public string RegexSource { get; }

    public IReadOnlyList<ReplPart> Repl { get; }

    public string ReplSource { get; }

    public bool Global { get; }

    public bool Print { get; }

    public int Count { get; }

    public string Text { get; }
}
