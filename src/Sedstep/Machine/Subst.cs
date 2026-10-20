using System.Text;

namespace Sedstep;

internal static class Subst
{
    internal static (string Text, bool Replaced) Apply(Cmd command, string original)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(original);

        Pattern pattern = command.Regex ?? throw new ArgumentException("Substitution command has no pattern.", nameof(command));
        StringBuilder result = new();
        int outputPosition = 0;
        int searchPosition = 0;
        int? previousEnd = null;
        int matchNumber = 0;
        bool replaced = false;

        while (pattern.Search(original, searchPosition) is Found match)
        {
            bool suppressedEmpty = match.Start == match.End && previousEnd == match.Start;
            previousEnd = match.End;

            if (suppressedEmpty)
            {
                searchPosition = AdvanceCodePoint(original, match.Start);
                if (searchPosition == match.Start)
                {
                    break;
                }

                continue;
            }

            matchNumber++;
            result.Append(original, outputPosition, match.Start - outputPosition);
            if (ShouldReplace(command, matchNumber))
            {
                AppendReplacement(result, command.Repl, match, original);
                replaced = true;
            }
            else
            {
                result.Append(original, match.Start, match.End - match.Start);
            }

            outputPosition = match.End;
            if (match.Start == match.End)
            {
                searchPosition = AdvanceCodePoint(original, match.Start);
                if (searchPosition == match.Start)
                {
                    break;
                }
            }
            else
            {
                searchPosition = match.End;
            }
        }

        if (!replaced)
        {
            return (original, false);
        }

        result.Append(original, outputPosition, original.Length - outputPosition);
        return (result.ToString(), true);
    }

    private static bool ShouldReplace(Cmd command, int matchNumber)
    {
        if (command.Count > 0)
        {
            return command.Global ? matchNumber >= command.Count : matchNumber == command.Count;
        }

        return command.Global || matchNumber == 1;
    }

    private static void AppendReplacement(
        StringBuilder result,
        IReadOnlyList<ReplPart> replacement,
        Found match,
        string original)
    {
        foreach (ReplPart part in replacement)
        {
            if (part.Group == -1)
            {
                result.Append(part.Text);
                continue;
            }

            (int Start, int End)? span = part.Group == 0
                ? (match.Start, match.End)
                : part.Group <= match.Groups.Count ? match.Groups[part.Group - 1] : null;
            if (span is { } capture)
            {
                result.Append(original, capture.Start, capture.End - capture.Start);
            }
        }
    }

    private static int AdvanceCodePoint(string text, int offset)
    {
        if (offset >= text.Length)
        {
            return text.Length;
        }

        return char.IsHighSurrogate(text[offset])
            && offset + 1 < text.Length
            && char.IsLowSurrogate(text[offset + 1])
                ? offset + 2
                : offset + 1;
    }
}
