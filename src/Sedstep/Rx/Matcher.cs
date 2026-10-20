namespace Sedstep;

internal static class Matcher
{
    internal static Found? Search(ParsedPattern pattern, string text, int from)
    {
        CodePoints input = CodePoints.Read(text);
        int start = input.FirstBoundaryAtOrAfter(Math.Max(0, from));
        if (start > input.Count)
        {
            return null;
        }

        Program program = Program.Compile(pattern.Root);
        List<Thread> seeds = [];
        Found? best = null;

        for (int position = start; position <= input.Count; position++)
        {
            seeds.Add(new Thread(program.Start, position, EmptyCaptures(), []));
            ClosureResult closure = Close(program, seeds, position, input.Count);
            if (closure.Match is Thread matched)
            {
                Found candidate = MakeFound(matched, position, input.Offsets, pattern.Groups);
                if (best is null
                    || candidate.Start < best.Start
                    || (candidate.Start == best.Start && candidate.End > best.End))
                {
                    best = candidate;
                }
            }

            seeds = [];
            if (position == input.Count)
            {
                break;
            }

            int value = input.Values[position];
            foreach (Thread thread in closure.Consuming)
            {
                Instruction instruction = program.Instructions[thread.ProgramCounter];
                if (Consumes(instruction, value))
                {
                    seeds.Add(AfterConsumption(thread, instruction.Next));
                }
            }
        }

        return best;
    }

    private static int[] EmptyCaptures()
    {
        int[] captures = new int[18];
        Array.Fill(captures, -1);
        return captures;
    }

    private static Found MakeFound(Thread thread, int endPosition, int[] offsets, int groupCount)
    {
        List<(int Start, int End)?> groups = [];
        for (int group = 0; group < groupCount; group++)
        {
            int start = thread.Captures[group * 2];
            int end = thread.Captures[(group * 2) + 1];
            groups.Add(start < 0 || end < 0 ? null : (offsets[start], offsets[end]));
        }

        return new Found(
            offsets[thread.Start],
            offsets[endPosition],
            groups.AsReadOnly());
    }

    private static bool Consumes(Instruction instruction, int value)
    {
        return instruction.Operation switch
        {
            Operation.Character => instruction.Value == value,
            Operation.Any => true,
            Operation.Bracket => MatchesBracket((BracketNode)instruction.Node!, value),
            _ => false,
        };
    }

    private static bool MatchesBracket(BracketNode bracket, int value)
    {
        bool found = false;
        foreach (BracketItem item in bracket.Items)
        {
            found |= item switch
            {
                CharacterItem character => character.Value == value,
                RangeItem range => value >= range.First && value <= range.Last,
                ClassItem characterClass => MatchesClass(characterClass.Class, value),
                _ => false,
            };
        }

        return bracket.Negated ? !found : found;
    }

    private static bool MatchesClass(CharacterClass characterClass, int value)
    {
        return characterClass switch
        {
            CharacterClass.Alpha => IsAsciiLetter(value),
            CharacterClass.Digit => value is >= '0' and <= '9',
            CharacterClass.Alnum => IsAsciiLetter(value) || value is >= '0' and <= '9',
            CharacterClass.Upper => value is >= 'A' and <= 'Z',
            CharacterClass.Lower => value is >= 'a' and <= 'z',
            CharacterClass.Space => value is ' ' or '\t' or '\n' or '\v' or '\f' or '\r',
            _ => false,
        };
    }

    private static bool IsAsciiLetter(int value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static Thread AfterConsumption(Thread thread, int next)
    {
        if (thread.Frames.Length == 0 || thread.Frames.All(frame => frame.Progress))
        {
            return thread with { ProgramCounter = next };
        }

        RepeatFrame[] frames = (RepeatFrame[])thread.Frames.Clone();
        for (int index = 0; index < frames.Length; index++)
        {
            frames[index] = frames[index] with { Progress = true };
        }

        return thread with { ProgramCounter = next, Frames = frames };
    }

    private static ClosureResult Close(Program program, IReadOnlyList<Thread> seeds, int position, int inputLength)
    {
        Stack<Thread> pending = new();
        for (int index = seeds.Count - 1; index >= 0; index--)
        {
            pending.Push(seeds[index]);
        }

        // Each boundary explores each program counter and active interval/progress state once.
        // Counts for unbounded repeats saturate at their minimum; bounded intervals remain
        // counters, not expanded copies. Thus work is linear in input boundaries times the
        // finite instruction/repeat-state bound, rather than the number of backtracking paths.
        // Captures and starts are omitted from identity: ordered seeds and greedy-first splits
        // ensure the first equivalent continuation has the earliest start and dominant captures.
        HashSet<StateIdentity> visited = [];
        List<Thread> consuming = [];
        Thread? firstMatch = null;

        while (pending.Count > 0)
        {
            Thread thread = pending.Pop();
            if (!visited.Add(new StateIdentity(thread.ProgramCounter, thread.Frames)))
            {
                continue;
            }

            Instruction instruction = program.Instructions[thread.ProgramCounter];
            switch (instruction.Operation)
            {
                case Operation.Character:
                case Operation.Any:
                case Operation.Bracket:
                    consuming.Add(thread);
                    break;
                case Operation.AssertStart:
                    if (position == 0)
                    {
                        pending.Push(thread with { ProgramCounter = instruction.Next });
                    }

                    break;
                case Operation.AssertEnd:
                    if (position == inputLength)
                    {
                        pending.Push(thread with { ProgramCounter = instruction.Next });
                    }

                    break;
                case Operation.SaveStart:
                    {
                        int[] captures = (int[])thread.Captures.Clone();
                        captures[instruction.Value * 2] = position;
                        captures[(instruction.Value * 2) + 1] = -1;
                        pending.Push(thread with { ProgramCounter = instruction.Next, Captures = captures });
                        break;
                    }

                case Operation.SaveEnd:
                    {
                        int[] captures = (int[])thread.Captures.Clone();
                        captures[(instruction.Value * 2) + 1] = position;
                        pending.Push(thread with { ProgramCounter = instruction.Next, Captures = captures });
                        break;
                    }

                case Operation.RepeatEnter:
                    ExpandRepeat(thread, instruction, position, pending);
                    break;
                case Operation.RepeatEnd:
                    FinishRepeat(thread, instruction, pending);
                    break;
                case Operation.Match:
                    firstMatch ??= thread;
                    break;
            }
        }

        return new ClosureResult(consuming, firstMatch);
    }

    private static void ExpandRepeat(Thread thread, Instruction instruction, int position, Stack<Thread> pending)
    {
        RepeatFrame[] frames = thread.Frames;
        RepeatFrame frame;

        if (frames.Length == 0 || frames[^1].Id != instruction.Value)
        {
            frame = new RepeatFrame(instruction.Value, 0, false);
            frames = PushFrame(frames, frame);
        }
        else
        {
            frame = frames[^1];
        }

        bool minimumRequired = frame.Count < instruction.Minimum;
        if (minimumRequired)
        {
            RepeatFrame[] bodyFrames = ReplaceTop(frames, frame with { Progress = false });
            pending.Push(thread with { ProgramCounter = instruction.Body, Frames = bodyFrames });
            return;
        }

        if (instruction.Maximum is int maximum && frame.Count >= maximum)
        {
            pending.Push(thread with { ProgramCounter = instruction.Next, Frames = PopFrame(frames) });
            return;
        }

        RepeatFrame[] nextIteration = ReplaceTop(frames, frame with { Progress = false });
        pending.Push(thread with { ProgramCounter = instruction.Next, Frames = PopFrame(frames) });
        pending.Push(thread with { ProgramCounter = instruction.Body, Frames = nextIteration });
    }

    private static void FinishRepeat(Thread thread, Instruction instruction, Stack<Thread> pending)
    {
        RepeatFrame[] frames = thread.Frames;
        RepeatFrame frame = frames[^1];
        Instruction enter = instruction.TargetInstruction!;

        int count = enter.Maximum.HasValue
            ? frame.Count + 1
            : Math.Min(enter.Minimum, frame.Count + 1);

        if (!frame.Progress && count >= enter.Minimum)
        {
            pending.Push(thread with { ProgramCounter = enter.Next, Frames = PopFrame(frames) });
            return;
        }

        RepeatFrame[] finished = ReplaceTop(frames, frame with { Count = count, Progress = false });
        pending.Push(thread with { ProgramCounter = instruction.Next, Frames = finished });
    }

    private static RepeatFrame[] PushFrame(RepeatFrame[] frames, RepeatFrame frame)
    {
        RepeatFrame[] result = new RepeatFrame[frames.Length + 1];
        Array.Copy(frames, result, frames.Length);
        result[^1] = frame;
        return result;
    }

    private static RepeatFrame[] ReplaceTop(RepeatFrame[] frames, RepeatFrame frame)
    {
        RepeatFrame[] result = (RepeatFrame[])frames.Clone();
        result[^1] = frame;
        return result;
    }

    private static RepeatFrame[] PopFrame(RepeatFrame[] frames)
    {
        RepeatFrame[] result = new RepeatFrame[frames.Length - 1];
        Array.Copy(frames, result, result.Length);
        return result;
    }

    private readonly record struct ClosureResult(List<Thread> Consuming, Thread? Match);

    private sealed record Thread(int ProgramCounter, int Start, int[] Captures, RepeatFrame[] Frames);

    private readonly record struct RepeatFrame(int Id, int Count, bool Progress);

    private readonly struct StateIdentity : IEquatable<StateIdentity>
    {
        private readonly int programCounter;
        private readonly RepeatFrame[] frames;

        internal StateIdentity(int programCounter, RepeatFrame[] frames)
        {
            this.programCounter = programCounter;
            this.frames = frames;
        }

        public bool Equals(StateIdentity other)
        {
            if (programCounter != other.programCounter || frames.Length != other.frames.Length)
            {
                return false;
            }

            for (int index = 0; index < frames.Length; index++)
            {
                if (frames[index] != other.frames[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is StateIdentity other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(programCounter);
            foreach (RepeatFrame frame in frames)
            {
                hash.Add(frame);
            }

            return hash.ToHashCode();
        }
    }

    private sealed class Program
    {
        private Program(List<Instruction> instructions, int start)
        {
            Instructions = instructions;
            Start = start;
        }

        internal IReadOnlyList<Instruction> Instructions { get; }

        internal int Start { get; }

        internal static Program Compile(Node root)
        {
            List<Instruction> instructions = [];
            int match = Emit(instructions, new Instruction(Operation.Match));
            int start = CompileNode(root, match, instructions);
            return new Program(instructions, start);
        }

        private static int CompileNode(Node node, int next, List<Instruction> instructions)
        {
            switch (node)
            {
                case SequenceNode sequence:
                    {
                        int start = next;
                        for (int index = sequence.Children.Count - 1; index >= 0; index--)
                        {
                            start = CompileNode(sequence.Children[index], start, instructions);
                        }

                        return start;
                    }

                case LiteralNode literal:
                    return Emit(instructions, new Instruction(Operation.Character, next: next, value: literal.Value));
                case AnyNode:
                    return Emit(instructions, new Instruction(Operation.Any, next: next));
                case AnchorNode anchor:
                    return Emit(instructions, new Instruction(
                        anchor.IsStart ? Operation.AssertStart : Operation.AssertEnd,
                        next: next));
                case BracketNode bracket:
                    return Emit(instructions, new Instruction(Operation.Bracket, next: next, node: bracket));
                case GroupNode group:
                    {
                        int saveEnd = Emit(instructions, new Instruction(Operation.SaveEnd, next: next, value: group.Number - 1));
                        int child = CompileNode(group.Child, saveEnd, instructions);
                        return Emit(instructions, new Instruction(Operation.SaveStart, next: child, value: group.Number - 1));
                    }

                case RepeatNode repeat:
                    {
                        int repeatId = instructions.Count;
                        int enter = Emit(instructions, new Instruction(
                            Operation.RepeatEnter,
                            next: next,
                            value: repeatId,
                            minimum: repeat.Minimum,
                            maximum: repeat.Maximum));
                        int end = Emit(instructions, new Instruction(
                            Operation.RepeatEnd,
                            next: enter,
                            value: repeatId));
                        int body = CompileNode(repeat.Child, end, instructions);
                        instructions[enter].Body = body;
                        instructions[end].TargetInstruction = instructions[enter];
                        return enter;
                    }

                default:
                    throw new InvalidOperationException("Unknown regular expression node.");
            }
        }

        private static int Emit(List<Instruction> instructions, Instruction instruction)
        {
            instructions.Add(instruction);
            return instructions.Count - 1;
        }
    }

    private enum Operation
    {
        Character,
        Any,
        Bracket,
        AssertStart,
        AssertEnd,
        SaveStart,
        SaveEnd,
        RepeatEnter,
        RepeatEnd,
        Match,
    }

    private sealed class Instruction
    {
        internal Instruction(
            Operation operation,
            int next = -1,
            int value = 0,
            int minimum = 0,
            int? maximum = null,
            Node? node = null)
        {
            Operation = operation;
            Next = next;
            Value = value;
            Minimum = minimum;
            Maximum = maximum;
            Node = node;
        }

        internal Operation Operation { get; }

        internal int Next { get; }

        internal int Value { get; }

        internal int Minimum { get; }

        internal int? Maximum { get; }

        internal Node? Node { get; }

        internal int Body { get; set; } = -1;

        internal Instruction? TargetInstruction { get; set; }
    }

    private sealed class CodePoints
    {
        private CodePoints(int[] values, int[] offsets)
        {
            Values = values;
            Offsets = offsets;
        }

        internal int[] Values { get; }

        internal int[] Offsets { get; }

        internal int Count => Values.Length;

        internal int FirstBoundaryAtOrAfter(int utf16Offset)
        {
            int lower = 0;
            int upper = Offsets.Length;
            while (lower < upper)
            {
                int middle = lower + ((upper - lower) / 2);
                if (Offsets[middle] < utf16Offset)
                {
                    lower = middle + 1;
                }
                else
                {
                    upper = middle;
                }
            }

            return lower;
        }

        internal static CodePoints Read(string text)
        {
            List<int> values = [];
            List<int> offsets = [0];

            for (int index = 0; index < text.Length;)
            {
                char first = text[index];
                int value;
                if (char.IsHighSurrogate(first)
                    && index + 1 < text.Length
                    && char.IsLowSurrogate(text[index + 1]))
                {
                    value = char.ConvertToUtf32(first, text[index + 1]);
                    index += 2;
                }
                else
                {
                    value = first;
                    index++;
                }

                values.Add(value);
                offsets.Add(index);
            }

            return new CodePoints(values.ToArray(), offsets.ToArray());
        }
    }
}
