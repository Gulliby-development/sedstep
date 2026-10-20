using System.Globalization;

namespace Sedstep;

public static class Explain
{
    public static string Meaning(Cmd command)
    {
        ArgumentNullException.ThrowIfNull(command);

        string action = Action(command);
        if (command.First is null)
        {
            return action;
        }

        string when = command.Second is null
            ? SingleAddress(command.First)
            : Range(command.First, command.Second);

        if (command.Negated)
        {
            when = when.StartsWith("on ", StringComparison.Ordinal)
                ? "except " + when[3..]
                : "outside " + when[3..];
        }

        return when + ", " + action;
    }

    private static string SingleAddress(Addr address) =>
        address.Kind switch
        {
            AddrKind.Line => "on line " + Number(address.A),
            AddrKind.Last => "on the last line",
            AddrKind.Regex => "on lines that match " + Text.Cell(address.Source),
            _ => throw new ArgumentOutOfRangeException(nameof(address), address.Kind, "Invalid first address."),
        };

    private static string Range(Addr first, Addr second)
    {
        string start = first.Kind switch
        {
            AddrKind.Line when first.A == 0 => "the start of the input",
            AddrKind.Line => "line " + Number(first.A),
            AddrKind.Last => "the last line",
            AddrKind.Regex => "a line that matches " + Text.Cell(first.Source),
            _ => throw new ArgumentOutOfRangeException(nameof(first), first.Kind, "Invalid first address."),
        };

        string end = second.Kind switch
        {
            AddrKind.Line => "line " + Number(second.A),
            AddrKind.Last => "the last line",
            AddrKind.Regex => "the next line that matches " + Text.Cell(second.Source),
            AddrKind.Plus when second.A == 0 => "the same line",
            AddrKind.Plus when second.A == 1 => "the next line",
            AddrKind.Plus => "the line " + Number(second.A) + " lines later",
            _ => throw new ArgumentOutOfRangeException(nameof(second), second.Kind, "Invalid second address."),
        };

        return "in the range from " + start + " to " + end;
    }

    private static string Action(Cmd command) =>
        command.Letter switch
        {
            '{' => "run the commands up to the matching }",
            ':' => "define the label " + command.Label,
            'b' => "jump to " + Target(command),
            't' => "jump to " + Target(command) + " if a substitution succeeded since the last line was read or the last t",
            'T' => "jump to " + Target(command) + " if no substitution succeeded since the last line was read or the last T",
            's' => Substitution(command),
            'a' => "queue the text " + Text.Cell(command.Text) + " to be printed at the end of the cycle",
            'i' => "print the text " + Text.Cell(command.Text) + " now",
            'd' => "delete the pattern space and start the next cycle",
            'D' => "delete up to the first newline of the pattern space and restart the cycle without reading a line",
            'p' => "print the pattern space",
            'P' => "print the pattern space up to its first newline",
            'n' => "print the pattern space (unless -n is given), then replace it with the next line; with no next line, end the script",
            'N' => "append a newline and the next line to the pattern space; with no next line, end the script",
            'g' => "replace the pattern space with the hold space",
            'G' => "append a newline and the hold space to the pattern space",
            'h' => "replace the hold space with the pattern space",
            'H' => "append a newline and the pattern space to the hold space",
            'x' => "swap the pattern space and the hold space",
            '=' => "print the line number",
            'q' => "print the pattern space (unless -n is given) and quit",
            'Q' => "quit without printing",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Letter, "Invalid command."),
        };

    private static string Substitution(Cmd command)
    {
        string which = (command.Count, command.Global) switch
        {
            (0, false) => "the first match",
            (0, true) => "every match",
            (_, false) => "match number " + Number(command.Count),
            (_, true) => "match number " + Number(command.Count) + " and every later match",
        };

        string result = "replace " + which + " of " + Text.Cell(command.RegexSource)
            + " with " + Text.Cell(command.ReplSource);
        return command.Print
            ? result + " and print the pattern space if a replacement was made"
            : result;
    }

    private static string Target(Cmd command) =>
        command.Label.Length == 0
            ? "the end of the script"
            : "the label " + command.Label;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
