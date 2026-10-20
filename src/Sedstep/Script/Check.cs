using System.Globalization;

namespace Sedstep;

public record Finding(int Command, string Code, string Message);

public static class Check
{
    public static List<Finding> Run(IReadOnlyList<Cmd> cmds)
    {
        ArgumentNullException.ThrowIfNull(cmds);

        HashSet<string> referencedLabels = new(StringComparer.Ordinal);
        foreach (Cmd command in cmds)
        {
            if ((command.Letter is 'b' or 't' or 'T') && command.Label.Length > 0)
            {
                referencedLabels.Add(command.Label);
            }
        }

        List<Finding> findings = [];
        Stack<int?> blockStates = [];
        int? latestTerminator = null;
        bool hasPriorSubstitution = false;

        foreach (Cmd command in cmds)
        {
            if (command.Letter == '}')
            {
                if (blockStates.Count > 0)
                {
                    latestTerminator = blockStates.Pop();
                }

                continue;
            }

            if (command.Letter == ':')
            {
                latestTerminator = null;
                if (!referencedLabels.Contains(command.Label))
                {
                    findings.Add(new Finding(
                        command.Number,
                        "SC002",
                        "label " + command.Label + " is never used"));
                }
            }
            else if (latestTerminator is int terminator)
            {
                findings.Add(new Finding(
                    command.Number,
                    "SC001",
                    "unreachable: command " + Number(terminator) + " always ends the cycle or jumps away"));
            }

            if (command.Letter == '{')
            {
                blockStates.Push(latestTerminator);
            }

            if ((command.Letter is 't' or 'T') && !hasPriorSubstitution)
            {
                findings.Add(new Finding(
                    command.Number,
                    "SC003",
                    command.Letter + " has no s command before it"));
            }

            if (command.First is { Kind: AddrKind.Line } first
                && command.Second is { Kind: AddrKind.Line } second
                && second.A < first.A)
            {
                findings.Add(new Finding(
                    command.Number,
                    "SC004",
                    "range " + Number(first.A) + "," + Number(second.A)
                    + " ends before it starts, so only line " + Number(first.A) + " matches"));
            }

            if (command.Letter == 's')
            {
                hasPriorSubstitution = true;
            }

            if (command.First is null
                && !command.Negated
                && (command.Letter is 'b' or 'd' or 'D' or 'q' or 'Q'))
            {
                latestTerminator = command.Number;
            }
        }

        findings.Sort(static (left, right) =>
        {
            int commandOrder = left.Command.CompareTo(right.Command);
            return commandOrder != 0
                ? commandOrder
                : StringComparer.Ordinal.Compare(left.Code, right.Code);
        });

        return findings;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
