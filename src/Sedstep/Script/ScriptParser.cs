namespace Sedstep;

public static class ScriptParser
{
    private const int MaximumScriptLength = 2000;
    private const int MaximumNumber = 999999;
    private static readonly char[] SubstitutionDelimiters = ['/', ',', ':', '#', '@', '%', '~', '!', '_', '='];

    public static IReadOnlyList<Cmd> Parse(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (CountCodePoints(script) > MaximumScriptLength)
        {
            throw new Failure("the script is longer than 2000 characters");
        }

        Parser parser = new(script);
        return parser.Parse();
    }

    private static int CountCodePoints(string text)
    {
        int count = 0;
        for (int position = 0; position < text.Length; count++)
        {
            position += CodePointLengthAt(text, position);
        }

        return count;
    }

    private static int CodePointLengthAt(string text, int position)
    {
        return char.IsHighSurrogate(text[position])
            && position + 1 < text.Length
            && char.IsLowSurrogate(text[position + 1])
                ? 2
                : 1;
    }

    private static int CodePointAt(string text, int position, out int length)
    {
        length = CodePointLengthAt(text, position);
        return length == 2
            ? char.ConvertToUtf32(text[position], text[position + 1])
            : text[position];
    }

    private sealed class Parser
    {
        private readonly string source;
        private readonly List<Draft> commands = [];
        private readonly Dictionary<string, int> labels = new(StringComparer.Ordinal);
        private readonly List<(string Name, int NamePosition)> branches = [];
        private readonly Stack<int> blocks = [];
        private int position;
        private int number;

        internal Parser(string source)
        {
            this.source = source;
        }

        internal IReadOnlyList<Cmd> Parse()
        {
            while (true)
            {
                SkipSeparators();
                if (position >= source.Length)
                {
                    break;
                }

                ParseCommand();
            }

            if (blocks.Count > 0)
            {
                int unmatched = commands[blocks.Peek()].Origin;
                throw Error("unmatched {", unmatched);
            }

            foreach ((string name, int namePosition) in branches)
            {
                if (!labels.ContainsKey(name))
                {
                    throw Error("can't find label " + name, namePosition);
                }
            }

            Cmd[] result = new Cmd[commands.Count];
            for (int index = 0; index < commands.Count; index++)
            {
                Draft draft = commands[index];
                int target = draft.Label.Length > 0 && IsBranch(draft.Letter)
                    ? labels[draft.Label]
                    : -1;
                result[index] = new Cmd(
                    draft.Letter,
                    draft.Number,
                    draft.First,
                    draft.Second,
                    draft.Negated,
                    draft.Label,
                    target,
                    draft.Jump,
                    draft.Regex,
                    draft.RegexSource,
                    draft.Repl,
                    draft.ReplSource,
                    draft.Global,
                    draft.Print,
                    draft.Count,
                    draft.Text);
            }

            return Array.AsReadOnly(result);
        }

        private void ParseCommand()
        {
            int commandStart = position;
            Addr? first = null;
            Addr? second = null;
            bool hasAddress = IsAddressStart(CurrentValue());
            if (hasAddress)
            {
                first = ParseAddress(allowPlus: false);
                if (TryConsume(','))
                {
                    second = ParseAddressAfterComma();
                }

                if (first.Kind == AddrKind.Line && first.A == 0
                    && (second is not { Kind: AddrKind.Regex }))
                {
                    throw Error("line address 0 is only valid as 0,/regex/", commandStart);
                }
            }

            SkipBlanks();
            bool negated = false;
            int negationPosition = position;
            if (TryConsume('!'))
            {
                if (!hasAddress)
                {
                    throw Error("! needs an address", negationPosition);
                }

                negated = true;
                SkipBlanks();
                if (position < source.Length && CurrentValue() == '!')
                {
                    throw Error("multiple !", position);
                }
            }

            if (position >= source.Length || CurrentValue() is ';' or '\n')
            {
                throw Error("missing command", position);
            }

            int letterPosition = position;
            int letterValue = CurrentValue();
            if (letterValue > char.MaxValue)
            {
                throw Error("unknown command " + Text.Show(CurrentText()), letterPosition);
            }

            char letter = (char)letterValue;
            Advance();
            if (!IsSupported(letter))
            {
                throw Error("unknown command " + Text.Show(letter.ToString()), letterPosition);
            }

            if (letter == '}' && hasAddress)
            {
                throw Error("} takes no address", letterPosition);
            }

            if (letter == ':' && hasAddress)
            {
                throw Error(": takes no address", letterPosition);
            }

            if (letter is 'q' or 'Q' && second is not null)
            {
                throw Error("q and Q take at most one address", commandStart);
            }

            Draft draft = new()
            {
                Letter = letter,
                Number = letter == '}' ? 0 : ++number,
                First = first,
                Second = second,
                Negated = negated,
                Origin = letterPosition,
            };

            switch (letter)
            {
                case '{':
                    commands.Add(draft);
                    blocks.Push(commands.Count - 1);
                    return;
                case '}':
                    if (blocks.Count == 0)
                    {
                        throw Error("unexpected }", letterPosition);
                    }

                    commands.Add(draft);
                    int openIndex = blocks.Pop();
                    commands[openIndex].Jump = commands.Count - 1;
                    EnsureCommandEnd();
                    return;
                case ':':
                    ParseLabel(draft, letterPosition);
                    return;
                case 'b':
                case 't':
                case 'T':
                    ParseBranch(draft);
                    return;
                case 's':
                    ParseSubstitution(draft, letterPosition);
                    return;
                case 'a':
                case 'i':
                    ParseText(draft);
                    return;
                default:
                    commands.Add(draft);
                    EnsureCommandEnd();
                    return;
            }
        }

        private Addr ParseAddressAfterComma()
        {
            if (position >= source.Length || !IsAddressStart(CurrentValue()) && CurrentValue() != '+')
            {
                throw Error("expected an address after ,", position);
            }

            return ParseAddress(allowPlus: true);
        }

        private Addr ParseAddress(bool allowPlus)
        {
            if (position >= source.Length)
            {
                throw Error("expected an address after ,", position);
            }

            int value = CurrentValue();
            if (value is >= '0' and <= '9')
            {
                int numberValue = ReadBoundedNumber(MaximumNumber, "number is larger than 999999");
                return new Addr(AddrKind.Line, numberValue, string.Empty, null);
            }

            if (value == '$')
            {
                Advance();
                return new Addr(AddrKind.Last, 0, string.Empty, null);
            }

            if (value == '/' && !allowPlus)
            {
                return ParseAddressRegex();
            }

            if (value == '/' && allowPlus)
            {
                return ParseAddressRegex();
            }

            if (value == '+' && allowPlus)
            {
                Advance();
                if (position >= source.Length || CurrentValue() is < '0' or > '9')
                {
                    throw Error("expected a number after +", position);
                }

                int plus = ReadBoundedNumber(MaximumNumber, "number is larger than 999999");
                return new Addr(AddrKind.Plus, plus, string.Empty, null);
            }

            throw Error("expected an address after ,", position);
        }

        private Addr ParseAddressRegex()
        {
            int openingPosition = position;
            Advance();
            int regexStart = position;
            List<DecodedChar> decoded = [];
            while (position < source.Length)
            {
                int value = CurrentValue();
                if (value == '\n')
                {
                    throw Error("unterminated address regex", openingPosition);
                }

                if (value == '/')
                {
                    Advance();
                    string regexSource = ToText(decoded);
                    Pattern pattern = ParsePattern(regexSource, regexStart);
                    return new Addr(AddrKind.Regex, 0, regexSource, pattern);
                }

                if (value == '\\')
                {
                    int slashPosition = position;
                    Advance();
                    if (position >= source.Length || CurrentValue() == '\n')
                    {
                        throw Error("unterminated address regex", openingPosition);
                    }

                    int escapedLength = CurrentLength();
                    int escapedValue = CurrentValue();
                    if (escapedValue == '/')
                    {
                        AddCodePoint(decoded, escapedValue, slashPosition, escapedLength);
                        Advance();
                    }
                    else
                    {
                        AddCodePoint(decoded, '\\', slashPosition, 1);
                        AddCurrentCodePoint(decoded);
                    }

                    continue;
                }

                AddCurrentCodePoint(decoded);
            }

            throw Error("unterminated address regex", openingPosition);
        }

        private void ParseLabel(Draft draft, int colonPosition)
        {
            if (draft.First is not null)
            {
                throw Error(": takes no address", colonPosition);
            }

            if (draft.Negated)
            {
                throw Error("! needs an address", colonPosition - 1);
            }

            int nameStart = position;
            string name = ReadLabel();
            if (name.Length == 0)
            {
                throw Error("missing label name", nameStart);
            }

            if (labels.ContainsKey(name))
            {
                throw Error("duplicate label " + name, nameStart);
            }

            draft.Label = name;
            labels.Add(name, commands.Count);
            commands.Add(draft);
            EnsureCommandEnd();
        }

        private void ParseBranch(Draft draft)
        {
            SkipBlanks();
            int namePosition = position;
            string name = position < source.Length && IsNamePart(CurrentValue()) ? ReadLabel() : string.Empty;
            draft.Label = name;
            commands.Add(draft);
            if (name.Length > 0)
            {
                branches.Add((name, namePosition));
            }

            EnsureCommandEnd();
        }

        private void ParseSubstitution(Draft draft, int commandPosition)
        {
            if (position >= source.Length)
            {
                throw Error("unterminated s command", commandPosition);
            }

            int delimiterPosition = position;
            int delimiterValue = CurrentValue();
            if (delimiterValue > char.MaxValue || !SubstitutionDelimiters.Contains((char)delimiterValue))
            {
                throw Error("invalid delimiter " + Text.Show(CurrentText()), delimiterPosition);
            }

            char delimiter = (char)delimiterValue;
            Advance();
            int regexStart = position;
            List<DecodedChar> regexChars = ReadSubstitutionField(delimiter, commandPosition);
            string regexSource = ToText(regexChars);
            draft.RegexSource = regexSource;
            draft.Regex = ParsePattern(regexSource, regexStart);

            int replacementStart = position;
            List<DecodedChar> replacementChars = ReadSubstitutionField(delimiter, commandPosition);
            draft.ReplSource = ToText(replacementChars);
            draft.Repl = ParseReplacement(replacementChars, replacementStart, draft.Regex.Groups);

            bool sawGlobal = false;
            bool sawPrint = false;
            bool sawCount = false;
            int count = 0;
            int countPosition = position;

            while (position < source.Length)
            {
                int value = CurrentValue();
                if (value is ' ' or '\t')
                {
                    break;
                }

                if (value is ';' or '\n' or '}')
                {
                    break;
                }

                if (value == 'g')
                {
                    if (sawGlobal)
                    {
                        throw Error("multiple g flags", position);
                    }

                    sawGlobal = true;
                    Advance();
                    continue;
                }

                if (value == 'p')
                {
                    if (sawPrint)
                    {
                        throw Error("multiple p flags", position);
                    }

                    sawPrint = true;
                    Advance();
                    continue;
                }

                if (value is >= '0' and <= '9')
                {
                    int currentNumberPosition = position;
                    if (sawCount)
                    {
                        throw Error("multiple number flags", currentNumberPosition);
                    }

                    sawCount = true;
                    countPosition = currentNumberPosition;
                    count = ReadBoundedNumber(999, "number is larger than 999");
                    if (count == 0)
                    {
                        throw Error("the number flag may not be zero", countPosition);
                    }

                    continue;
                }

                throw Error("unknown s flag " + Text.Show(CurrentText()), position);
            }

            draft.Global = sawGlobal;
            draft.Print = sawPrint;
            draft.Count = count;
            commands.Add(draft);
            EnsureCommandEnd();
        }

        private List<DecodedChar> ReadSubstitutionField(char delimiter, int commandPosition)
        {
            List<DecodedChar> result = [];
            while (position < source.Length)
            {
                int value = CurrentValue();
                if (value == delimiter)
                {
                    Advance();
                    return result;
                }

                if (value == '\n')
                {
                    throw Error("unterminated s command", commandPosition);
                }

                if (value == '\\')
                {
                    int slashPosition = position;
                    Advance();
                    if (position >= source.Length)
                    {
                        throw Error("unterminated s command", commandPosition);
                    }

                    int escaped = CurrentValue();
                    if (escaped == '\n')
                    {
                        result.Add(new DecodedChar('\\', slashPosition));
                        result.Add(new DecodedChar('n', position));
                        Advance();
                    }
                    else if (escaped == delimiter)
                    {
                        int length = CurrentLength();
                        AddCodePoint(result, escaped, slashPosition, length);
                        Advance();
                    }
                    else
                    {
                        result.Add(new DecodedChar('\\', slashPosition));
                        AddCurrentCodePoint(result);
                    }

                    continue;
                }

                AddCurrentCodePoint(result);
            }

            throw Error("unterminated s command", commandPosition);
        }

        private IReadOnlyList<ReplPart> ParseReplacement(
            IReadOnlyList<DecodedChar> sourceChars,
            int replacementStart,
            int groups)
        {
            List<ReplPart> parts = [];
            System.Text.StringBuilder literal = new();

            void FlushLiteral()
            {
                if (literal.Length > 0)
                {
                    parts.Add(new ReplPart(-1, literal.ToString()));
                    literal.Clear();
                }
            }

            for (int index = 0; index < sourceChars.Count; index++)
            {
                DecodedChar current = sourceChars[index];
                if (current.Value == '&')
                {
                    FlushLiteral();
                    parts.Add(new ReplPart(0, string.Empty));
                    continue;
                }

                if (current.Value != '\\')
                {
                    literal.Append((char)current.Value);
                    continue;
                }

                if (index + 1 >= sourceChars.Count)
                {
                    literal.Append('\\');
                    continue;
                }

                int escapedIndex = index + 1;
                int escapedLength = 1;
                int escapedValue = sourceChars[escapedIndex].Value;
                if (char.IsHighSurrogate((char)escapedValue)
                    && escapedIndex + 1 < sourceChars.Count
                    && char.IsLowSurrogate(sourceChars[escapedIndex + 1].Value))
                {
                    escapedValue = char.ConvertToUtf32((char)escapedValue, sourceChars[escapedIndex + 1].Value);
                    escapedLength = 2;
                }

                index += escapedLength;
                if (escapedValue is >= '1' and <= '9')
                {
                    int group = escapedValue - '0';
                    if (group > groups)
                    {
                        throw Error(
                            "invalid reference \\" + (char)escapedValue + " on s command's RHS",
                            replacementStart);
                    }

                    FlushLiteral();
                    parts.Add(new ReplPart(group, string.Empty));
                    continue;
                }

                if (escapedValue == 'n')
                {
                    literal.Append('\n');
                    continue;
                }

                if (escapedValue == 't')
                {
                    literal.Append('\t');
                    continue;
                }

                if (IsLetterOrDigit(escapedValue))
                {
                    throw Error("unsupported escape \\" + CodePointString(escapedValue), current.SourcePosition);
                }

                literal.Append(CodePointString(escapedValue));
            }

            FlushLiteral();
            return Array.AsReadOnly(parts.ToArray());
        }

        private void ParseText(Draft draft)
        {
            SkipBlanks();
            int textStart = position;
            while (position < source.Length && CurrentValue() != '\n')
            {
                if (CurrentValue() == '\\')
                {
                    throw Error("backslash in text is not supported", position);
                }

                Advance();
            }

            if (position == textStart)
            {
                throw Error("expected text after a or i", textStart);
            }

            draft.Text = source.Substring(textStart, position - textStart);
            commands.Add(draft);
        }

        private void EnsureCommandEnd()
        {
            SkipBlanks();
            if (position < source.Length && CurrentValue() is not (';' or '\n' or '}'))
            {
                throw Error("extra characters after command", position);
            }
        }

        private int ReadBoundedNumber(int maximum, string message)
        {
            int result = 0;
            while (position < source.Length && CurrentValue() is >= '0' and <= '9')
            {
                int digitPosition = position;
                int digit = CurrentValue() - '0';
                if (result > (maximum - digit) / 10)
                {
                    throw Error(message, digitPosition);
                }

                result = (result * 10) + digit;
                Advance();
            }

            return result;
        }

        private string ReadLabel()
        {
            int start = position;
            while (position < source.Length && IsNamePart(CurrentValue()))
            {
                Advance();
            }

            return source.Substring(start, position - start);
        }

        private void SkipSeparators()
        {
            while (position < source.Length && CurrentValue() is ' ' or '\t' or ';' or '\n')
            {
                Advance();
            }
        }

        private void SkipBlanks()
        {
            while (position < source.Length && CurrentValue() is ' ' or '\t')
            {
                Advance();
            }
        }

        private bool TryConsume(int value)
        {
            if (position >= source.Length || CurrentValue() != value)
            {
                return false;
            }

            Advance();
            return true;
        }

        private int CurrentValue() => CodePointAt(source, position, out _);

        private int CurrentLength() => CodePointLengthAt(source, position);

        private string CurrentText()
        {
            int length = CurrentLength();
            return source.Substring(position, length);
        }

        private void AddCurrentCodePoint(List<DecodedChar> result)
        {
            int value = CurrentValue();
            int sourcePosition = position;
            int length = CurrentLength();
            AddCodePoint(result, value, sourcePosition, length);
            Advance();
        }

        private static void AddCodePoint(List<DecodedChar> result, int value, int sourcePosition, int length)
        {
            if (length == 2)
            {
                string pair = char.ConvertFromUtf32(value);
                result.Add(new DecodedChar(pair[0], sourcePosition));
                result.Add(new DecodedChar(pair[1], sourcePosition + 1));
            }
            else
            {
                result.Add(new DecodedChar((char)value, sourcePosition));
            }
        }

        private static string ToText(IReadOnlyList<DecodedChar> chars)
        {
            System.Text.StringBuilder result = new();
            foreach (DecodedChar character in chars)
            {
                result.Append(character.Value);
            }

            return result.ToString();
        }

        private Pattern ParsePattern(string regexSource, int sourcePosition)
        {
            try
            {
                return Pattern.Parse(regexSource);
            }
            catch (Failure failure)
            {
                throw Error(failure.Message, sourcePosition);
            }
        }

        private Failure Error(string message, int sourcePosition)
        {
            int line = 1;
            int column = 1;
            for (int index = 0; index < sourcePosition && index < source.Length;)
            {
                int value = CodePointAt(source, index, out int length);
                if (value == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }

                index += length;
            }

            return new Failure(message + " at line " + line.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " column " + column.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }


        private void Advance() => position += CurrentLength();

        private static bool IsAddressStart(int value) =>
            value is >= '0' and <= '9' or '$' or '/';

        private static bool IsSupported(char letter) =>
            letter is '{' or '}' or ':' or 'b' or 't' or 'T' or 'd' or 'D' or 'p' or 'P'
                or 'n' or 'N' or 'g' or 'G' or 'h' or 'H' or 'x' or '=' or 'q' or 'Q'
                or 's' or 'a' or 'i';

        private static bool IsBranch(char letter) => letter is 'b' or 't' or 'T';

        private static bool IsNameStart(int value) => IsAsciiLetter(value) || value == '_';

        private static bool IsNamePart(int value) => IsNameStart(value) || value is >= '0' and <= '9';

        private static bool IsAsciiLetter(int value) =>
            value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

        private static bool IsLetterOrDigit(int value)
        {
            if (value <= char.MaxValue)
            {
                return char.IsLetterOrDigit((char)value);
            }

            return System.Text.Rune.IsLetterOrDigit(new System.Text.Rune(value));
        }

        private static string CodePointString(int value) =>
            value <= char.MaxValue ? ((char)value).ToString() : char.ConvertFromUtf32(value);
    }

    private sealed class Draft
    {
        internal char Letter { get; init; }

        internal int Number { get; init; }

        internal Addr? First { get; init; }

        internal Addr? Second { get; init; }

        internal bool Negated { get; init; }

        internal string Label { get; set; } = string.Empty;

        internal int Jump { get; set; } = -1;

        internal Pattern? Regex { get; set; }

        internal string RegexSource { get; set; } = string.Empty;

        internal IReadOnlyList<ReplPart> Repl { get; set; } = Array.Empty<ReplPart>();

        internal string ReplSource { get; set; } = string.Empty;

        internal bool Global { get; set; }

        internal bool Print { get; set; }

        internal int Count { get; set; }

        internal string Text { get; set; } = string.Empty;

        internal int Origin { get; init; }
    }

    private readonly record struct DecodedChar(char Value, int SourcePosition);
}
