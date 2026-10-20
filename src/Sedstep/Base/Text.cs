using System.Text;

namespace Sedstep;

public static class Text
{
    public static string Show(string s) => Render(s, '"', '"', escape: false);

    public static string Cell(string s) => Render(s, '[', ']', escape: false);

    public static string Esc(string s) => Render(s, '[', ']', escape: true);

    public static string Line(string key, string value)
    {
        int padding = Math.Max(0, 10 - Width(key));
        return key + new string(' ', padding) + "  " + value;
    }

    public static List<string> Table(
        IReadOnlyList<string> header,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        int[] widths = new int[header.Count];

        for (int column = 0; column < header.Count; column++)
        {
            widths[column] = Width(header[column]);
        }

        foreach (IReadOnlyList<string> row in rows)
        {
            for (int column = 0; column < header.Count; column++)
            {
                widths[column] = Math.Max(widths[column], Width(row[column]));
            }
        }

        List<string> result = new(rows.Count + 1)
        {
            FormatRow(header, widths),
        };

        foreach (IReadOnlyList<string> row in rows)
        {
            result.Add(FormatRow(row, widths));
        }

        return result;
    }

    private static string Render(string s, char opening, char closing, bool escape)
    {
        StringBuilder result = new();
        result.Append(opening);

        foreach (Rune rune in s.EnumerateRunes())
        {
            int value = rune.Value;
            if (escape && value == '\\')
            {
                result.Append("\\\\");
            }
            else if (escape && value == '\n')
            {
                result.Append("\\n");
            }
            else if (escape && value == '\t')
            {
                result.Append("\\t");
            }
            else if (value is >= 0x20 and <= 0x7E)
            {
                result.Append(rune.ToString());
            }
            else
            {
                result.Append('?');
            }
        }

        result.Append(closing);
        return result.ToString();
    }

    private static int Width(string s)
    {
        int width = 0;
        foreach (Rune _ in s.EnumerateRunes())
        {
            width++;
        }

        return width;
    }

    private static string FormatRow(IReadOnlyList<string> row, IReadOnlyList<int> widths)
    {
        StringBuilder result = new();

        for (int column = 0; column < widths.Count; column++)
        {
            string cell = row[column];
            result.Append(cell);

            if (column < widths.Count - 1)
            {
                result.Append(' ', widths[column] - Width(cell) + 2);
            }
        }

        return result.ToString().TrimEnd(' ');
    }
}
