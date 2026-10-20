namespace Sedstep;

public record Split(IReadOnlyList<string> Lines, bool FinalEol);

public static class Lines
{
    public static Split Of(string text)
    {
        if (text.Length == 0)
        {
            return new Split(Array.Empty<string>(), true);
        }

        List<string> lines = [];
        int lineStart = 0;

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lines.Add(text[lineStart..index]);
                lineStart = index + 1;
            }
        }

        bool finalEol = text[^1] == '\n';
        if (lineStart < text.Length)
        {
            lines.Add(text[lineStart..]);
        }

        return new Split(lines, finalEol);
    }
}
