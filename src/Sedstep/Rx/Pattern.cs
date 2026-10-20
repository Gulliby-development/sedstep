namespace Sedstep;

public sealed class Pattern
{
    private readonly ParsedPattern parsed;

    private Pattern(ParsedPattern parsed)
    {
        this.parsed = parsed;
    }

    public int Groups => parsed.Groups;

    public static Pattern Parse(string source) => new(RxParser.Parse(source));

    public Found? Search(string text, int from) => Matcher.Search(parsed, text, from);
}

public sealed record Found(int Start, int End, IReadOnlyList<(int Start, int End)?> Groups);
