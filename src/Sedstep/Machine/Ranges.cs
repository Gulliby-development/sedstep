namespace Sedstep;

internal sealed class Ranges
{
    private enum RangeStatus
    {
        Inactive,
        Active,
        Closed,
    }

    private struct RangeState
    {
        internal RangeStatus Status;
        internal int StartedAt;
        internal long EndLine;
    }

    private readonly RangeState[] states;

    internal Ranges(IReadOnlyList<Cmd> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        states = new RangeState[commands.Count];
        for (int index = 0; index < commands.Count; index++)
        {
            Cmd command = commands[index];
            if (command.First is { Kind: AddrKind.Line, A: 0 }
                && command.Second is { Kind: AddrKind.Regex })
            {
                states[index].Status = RangeStatus.Active;
            }
        }
    }

    internal bool Selects(int commandIndex, Cmd command, int line, bool hasMoreLines, string pattern)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(pattern);

        if (command.First is not Addr first)
        {
            return true;
        }

        bool selected;
        if (command.Second is not Addr second)
        {
            selected = Matches(first, line, hasMoreLines, pattern);
        }
        else
        {
            if ((uint)commandIndex >= (uint)states.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(commandIndex));
            }

            ref RangeState state = ref states[commandIndex];
            selected = SelectsRange(ref state, first, second, line, hasMoreLines, pattern);
        }

        return command.Negated ? !selected : selected;
    }

    private static bool SelectsRange(
        ref RangeState state,
        Addr first,
        Addr second,
        int line,
        bool hasMoreLines,
        string pattern)
    {
        if (state.Status == RangeStatus.Closed)
        {
            return false;
        }

        if (state.Status == RangeStatus.Active)
        {
            return ContinueRange(ref state, first, second, line, hasMoreLines, pattern);
        }

        if (first.Kind == AddrKind.Line)
        {
            if (line < first.A)
            {
                return false;
            }

            if (second.Kind == AddrKind.Line && line > first.A && line > second.A)
            {
                return false;
            }
        }
        else if (!Matches(first, line, hasMoreLines, pattern))
        {
            return false;
        }

        state.Status = RangeStatus.Active;
        state.StartedAt = line;

        switch (second.Kind)
        {
            case AddrKind.Line:
                state.EndLine = second.A;
                if (state.EndLine <= line)
                {
                    EndRange(ref state, first);
                }

                break;
            case AddrKind.Plus:
                state.EndLine = (long)line + second.A;
                if (state.EndLine <= line)
                {
                    EndRange(ref state, first);
                }

                break;
            case AddrKind.Last:
                if (!hasMoreLines)
                {
                    EndRange(ref state, first);
                }

                break;
            case AddrKind.Regex:
                // The ending address is not tested on the line that starts the range.
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(second), second.Kind, "Unknown address kind.");
        }

        return true;
    }

    private static bool ContinueRange(
        ref RangeState state,
        Addr first,
        Addr second,
        int line,
        bool hasMoreLines,
        string pattern)
    {
        switch (second.Kind)
        {
            case AddrKind.Line:
                if (line > state.EndLine)
                {
                    EndRange(ref state, first);
                    return false;
                }

                if (line == state.EndLine)
                {
                    EndRange(ref state, first);
                }

                return true;
            case AddrKind.Plus:
                if (line >= state.EndLine)
                {
                    EndRange(ref state, first);
                }

                return true;
            case AddrKind.Last:
                if (!hasMoreLines)
                {
                    EndRange(ref state, first);
                }

                return true;
            case AddrKind.Regex:
                if (line > state.StartedAt && Matches(second, line, hasMoreLines, pattern))
                {
                    EndRange(ref state, first);
                }

                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(second), second.Kind, "Unknown address kind.");
        }
    }

    private static void EndRange(ref RangeState state, Addr first)
    {
        state.Status = first.Kind == AddrKind.Line ? RangeStatus.Closed : RangeStatus.Inactive;
        state.StartedAt = 0;
        state.EndLine = 0;
    }

    private static bool Matches(Addr address, int line, bool hasMoreLines, string pattern)
    {
        return address.Kind switch
        {
            AddrKind.Line => line == address.A,
            AddrKind.Last => !hasMoreLines,
            AddrKind.Regex => address.Pattern?.Search(pattern, 0) is not null,
            AddrKind.Plus => false,
            _ => false,
        };
    }
}

