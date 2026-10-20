using System.Globalization;

namespace Sedstep;

public static class Machine
{
    public static Execution Execute(IReadOnlyList<Cmd> cmds, string text, bool quiet, bool trace)
    {
        ArgumentNullException.ThrowIfNull(cmds);
        ArgumentNullException.ThrowIfNull(text);

        Split input = Lines.Of(text);
        Space pattern = Space.Pattern();
        Space hold = Space.Hold();
        OutputBuffer output = new();
        TraceCollector traceCollector = new(trace);
        Ranges ranges = new(cmds);
        int nextLine = 0;
        int lineNumber = 0;
        int steps = 0;
        int cycles = 0;
        bool substituted = false;
        bool finished = false;

        while (!finished && nextLine < input.Lines.Count)
        {
            output.FlushQueue();
            ReadNextLine(input, ref nextLine, ref lineNumber, pattern);
            substituted = false;
            cycles++;

            int commandIndex = 0;
            bool nextCycle = false;
            while (!finished && !nextCycle && commandIndex < cmds.Count)
            {
                Cmd command = cmds[commandIndex];
                if (command.Letter is ':' or '}')
                {
                    commandIndex++;
                    continue;
                }

                bool hasMoreLines = nextLine < input.Lines.Count;
                if (!ranges.Selects(commandIndex, command, lineNumber, hasMoreLines, pattern.Text))
                {
                    commandIndex = command.Letter == '{' ? command.Jump + 1 : commandIndex + 1;
                    continue;
                }

                if (steps >= 100000)
                {
                    throw new Failure("the command limit of 100000 is exceeded");
                }

                steps++;
                int mark = traceCollector.Mark(output);
                int nextCommandIndex = commandIndex + 1;

                switch (command.Letter)
                {
                    case '{':
                        break;
                    case '}':
                    case ':':
                        break;
                    case 's':
                        {
                            (string replacedText, bool didReplace) = Subst.Apply(command, pattern.Text);
                            if (didReplace)
                            {
                                pattern.Set(replacedText, pattern.Eol);
                                substituted = true;
                                if (command.Print)
                                {
                                    output.Print(pattern);
                                }
                            }

                            break;
                        }
                    case 'd':
                        pattern.Clear();
                        output.FlushQueue();
                        nextCycle = true;
                        break;
                    case 'D':
                        {
                            int newline = pattern.Text.IndexOf('\n');
                            if (newline < 0)
                            {
                                pattern.Clear();
                                output.FlushQueue();
                                nextCycle = true;
                            }
                            else
                            {
                                pattern.Set(pattern.Text[(newline + 1)..], pattern.Eol);
                                output.FlushQueue();
                                nextCommandIndex = 0;
                            }

                            break;
                        }
                    case 'p':
                        output.Print(pattern);
                        break;
                    case 'P':
                        output.PrintFirst(pattern);
                        break;
                    case 'n':
                        if (nextLine >= input.Lines.Count)
                        {
                            CompleteCycle(output, pattern, quiet);
                            finished = true;
                        }
                        else
                        {
                            if (!quiet)
                            {
                                output.Print(pattern);
                            }

                            output.FlushQueue();
                            ReadNextLine(input, ref nextLine, ref lineNumber, pattern);
                            substituted = false;
                        }

                        break;
                    case 'N':
                        if (nextLine >= input.Lines.Count)
                        {
                            CompleteCycle(output, pattern, quiet);
                            finished = true;
                        }
                        else
                        {
                            output.FlushQueue();
                            string oldText = pattern.Text;
                            ReadNextLine(input, ref nextLine, ref lineNumber, out string nextText, out bool nextEol);
                            pattern.Set(string.Concat(oldText, "\n", nextText), nextEol);
                            substituted = false;
                        }

                        break;
                    case 'g':
                        pattern.CopyFrom(hold);
                        break;
                    case 'G':
                        pattern.Append(string.Concat("\n", hold.Text), hold.Eol);
                        break;
                    case 'h':
                        hold.CopyFrom(pattern);
                        break;
                    case 'H':
                        hold.Append(string.Concat("\n", pattern.Text), pattern.Eol);
                        break;
                    case 'x':
                        pattern.Swap(hold);
                        break;
                    case '=':
                        output.WriteLine(lineNumber.ToString(CultureInfo.InvariantCulture));
                        break;
                    case 'a':
                        output.Queue(command.Text);
                        break;
                    case 'i':
                        output.WriteLine(command.Text);
                        break;
                    case 'q':
                        if (!quiet)
                        {
                            output.Print(pattern);
                        }

                        output.FlushPendingLf();
                        output.FlushQueue();
                        finished = true;
                        break;
                    case 'Q':
                        finished = true;
                        break;
                    case 'b':
                        nextCommandIndex = BranchTarget(command, cmds.Count);
                        break;
                    case 't':
                        if (substituted)
                        {
                            substituted = false;
                            nextCommandIndex = BranchTarget(command, cmds.Count);
                        }

                        break;
                    case 'T':
                        if (!substituted)
                        {
                            nextCommandIndex = BranchTarget(command, cmds.Count);
                        }
                        else
                        {
                            substituted = false;
                        }

                        break;
                }

                traceCollector.RecordCommand(lineNumber, command, pattern, hold, output, mark);
                commandIndex = nextCommandIndex;
            }

            if (!finished && !nextCycle)
            {
                int mark = traceCollector.Mark(output);
                CompleteCycle(output, pattern, quiet);
                traceCollector.RecordEnd(lineNumber, pattern, hold, output, mark);
            }
        }

        return new Execution(output.Text, traceCollector.Rows, steps, cycles);
    }

    private static int BranchTarget(Cmd command, int commandCount) =>
        command.Target < 0 ? commandCount : command.Target;

    private static void CompleteCycle(OutputBuffer output, Space pattern, bool quiet)
    {
        if (!quiet)
        {
            output.Print(pattern);
        }

        output.FlushQueue();
    }

    private static void ReadNextLine(
        Split input,
        ref int nextLine,
        ref int lineNumber,
        Space pattern)
    {
        ReadNextLine(input, ref nextLine, ref lineNumber, out string text, out bool eol);
        pattern.Set(text, eol);
    }

    private static void ReadNextLine(
        Split input,
        ref int nextLine,
        ref int lineNumber,
        out string text,
        out bool eol)
    {
        int currentLine = nextLine++;
        lineNumber++;
        text = input.Lines[currentLine];
        eol = currentLine < input.Lines.Count - 1 || input.FinalEol;
    }
}
