using System.Text;

namespace Sedstep;

internal static class RxParser
{
    internal static ParsedPattern Parse(string? source)
    {
        string text = source ?? string.Empty;
        if (text.Length == 0)
        {
            throw new Failure("empty regular expression");
        }

        if (CountCodePoints(text) > 100)
        {
            throw new Failure("regular expression is longer than 100 characters");
        }

        Parser parser = new(text);
        return new ParsedPattern(parser.Parse(), parser.GroupCount);
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
        char first = text[position];
        return char.IsHighSurrogate(first)
            && position + 1 < text.Length
            && char.IsLowSurrogate(text[position + 1])
                ? 2
                : 1;
    }

    private static int CodePointAt(string text, int position, out int length)
    {
        length = CodePointLengthAt(text, position);
        char first = text[position];
        if (length == 2)
        {
            return char.ConvertToUtf32(first, text[position + 1]);
        }

        return first;
    }

    private sealed class Parser
    {
        private readonly string source;

        internal Parser(string source)
        {
            this.source = source;
        }
        private int position;

        internal int GroupCount { get; private set; }

        internal Node Parse()
        {
            List<Node> children = ParseSequence(inGroup: false);
            return new SequenceNode(children.ToArray());
        }

        private List<Node> ParseSequence(bool inGroup)
        {
            List<Node> children = [];

            while (position < source.Length)
            {
                int current = CurrentCodePoint();
                if (current == ')')
                {
                    if (inGroup)
                    {
                        break;
                    }

                    throw new Failure("unmatched )");
                }

                Node atom = ParseAtom();
                children.Add(ParseRepetition(atom));
            }

            return children;
        }

        private Node ParseAtom()
        {
            int value = CurrentCodePoint();

            switch (value)
            {
                case '(':
                    return ParseGroup();
                case '.':
                    Advance();
                    return new AnyNode();
                case '^':
                    Advance();
                    return new AnchorNode(IsStart: true);
                case '$':
                    Advance();
                    return new AnchorNode(IsStart: false);
                case '[':
                    return ParseBracket();
                case '\\':
                    return ParseEscape();
                case '|':
                    throw new Failure("alternation is not supported");
                case '*':
                case '+':
                case '?':
                case '{':
                    throw new Failure("nothing to repeat");
                default:
                    Advance();
                    return new LiteralNode(value);
            }
        }

        private Node ParseGroup()
        {
            Advance();
            int number = ++GroupCount;
            if (number > 9)
            {
                throw new Failure("more than 9 groups");
            }

            List<Node> children = ParseSequence(inGroup: true);
            if (position >= source.Length || CurrentCodePoint() != ')')
            {
                throw new Failure("unmatched (");
            }

            Advance();
            return new GroupNode(number, new SequenceNode(children.ToArray()));
        }

        private Node ParseEscape()
        {
            int escapeStart = position;
            Advance();
            if (position >= source.Length)
            {
                throw new Failure("trailing backslash");
            }

            int escaped = CurrentCodePoint();
            int escapedLength = CurrentCodePointLength();

            if (escaped == 'n')
            {
                Advance();
                return new LiteralNode('\n');
            }

            if (escaped == 't')
            {
                Advance();
                return new LiteralNode('\t');
            }

            if (escaped is >= '1' and <= '9')
            {
                throw new Failure("backreferences are not supported");
            }

            if (IsLetterOrDigit(escaped))
            {
                position += escapedLength;
                string sequence = source.Substring(escapeStart, position - escapeStart);
                throw new Failure("unsupported escape " + sequence);
            }

            position += escapedLength;
            return new LiteralNode(escaped);
        }

        private Node ParseRepetition(Node atom)
        {
            if (position >= source.Length)
            {
                return atom;
            }

            int value = CurrentCodePoint();
            if (value is not ('*' or '+' or '?') && value != '{')
            {
                return atom;
            }

            if (atom is AnchorNode)
            {
                throw new Failure("nothing to repeat");
            }

            if (value == '{')
            {
                return ParseInterval(atom);
            }

            Advance();
            return value switch
            {
                '*' => new RepeatNode(atom, Minimum: 0, Maximum: null),
                '+' => new RepeatNode(atom, Minimum: 1, Maximum: null),
                _ => new RepeatNode(atom, Minimum: 0, Maximum: 1),
            };
        }

        private Node ParseInterval(Node atom)
        {
            Advance();
            if (!TryReadNumber(out int minimum))
            {
                throw new Failure("invalid interval");
            }

            int? maximum;
            if (TryConsume('}'))
            {
                maximum = minimum;
            }
            else if (TryConsume(','))
            {
                if (TryConsume('}'))
                {
                    maximum = null;
                }
                else if (TryReadNumber(out int parsedMaximum) && TryConsume('}'))
                {
                    maximum = parsedMaximum;
                }
                else
                {
                    throw new Failure("invalid interval");
                }
            }
            else
            {
                throw new Failure("invalid interval");
            }

            if (minimum > 99 || maximum > 99)
            {
                throw new Failure("interval is larger than 99");
            }

            if (maximum.HasValue && minimum > maximum.Value)
            {
                throw new Failure("invalid interval");
            }

            return new RepeatNode(atom, minimum, maximum);
        }

        private bool TryReadNumber(out int number)
        {
            number = 0;
            bool foundDigit = false;

            while (position < source.Length)
            {
                int value = CurrentCodePoint();
                if (value is < '0' or > '9')
                {
                    break;
                }

                foundDigit = true;
                number = Math.Min(1000, (number * 10) + value - '0');
                Advance();
            }

            return foundDigit;
        }

        private Node ParseBracket()
        {
            Advance();
            bool negated = TryConsume('^');
            List<BracketToken> tokens = [];

            if (TryConsume(']'))
            {
                tokens.Add(BracketToken.ForCharacter(']'));
            }

            bool closed = false;
            while (position < source.Length)
            {
                if (CurrentCodePoint() == ']')
                {
                    Advance();
                    closed = true;
                    break;
                }

                tokens.Add(ParseBracketToken());
            }

            if (!closed)
            {
                throw new Failure("unterminated [");
            }

            List<BracketItem> items = [];
            for (int index = 0; index < tokens.Count;)
            {
                if (index + 2 < tokens.Count
                    && tokens[index].Character is int first
                    && tokens[index + 1].Character == '-'
                    && tokens[index + 2].Character is int last
                    && !(index == 0 && first == '-'))
                {
                    if (first > last)
                    {
                        throw new Failure("invalid range");
                    }

                    items.Add(new RangeItem(first, last));
                    index += 3;
                    continue;
                }

                BracketToken token = tokens[index++];
                items.Add(token.Character is int character
                    ? new CharacterItem(character)
                    : new ClassItem(token.Class!.Value));
            }

            return new BracketNode(negated, items.ToArray());
        }

        private BracketToken ParseBracketToken()
        {
            int value = CurrentCodePoint();
            if (value == '[' && NextCodePointIs(':'))
            {
                return ParseCharacterClass();
            }

            if (value == '\\')
            {
                Advance();
                if (position >= source.Length)
                {
                    throw new Failure("backslash in a bracket expression is not supported");
                }

                int escaped = CurrentCodePoint();
                Advance();
                return escaped switch
                {
                    'n' => BracketToken.ForCharacter('\n'),
                    't' => BracketToken.ForCharacter('\t'),
                    _ => throw new Failure("backslash in a bracket expression is not supported"),
                };
            }

            Advance();
            return BracketToken.ForCharacter(value);
        }

        private BracketToken ParseCharacterClass()
        {
            Advance();
            Advance();
            int nameStart = position;

            while (position < source.Length
                && !(CurrentCodePoint() == ':' && NextCodePointIs(']')))
            {
                if (CurrentCodePoint() == ']')
                {
                    throw new Failure("unterminated [");
                }

                Advance();
            }

            if (position >= source.Length)
            {
                throw new Failure("unterminated [");
            }

            string name = source.Substring(nameStart, position - nameStart);
            Advance();
            Advance();

            CharacterClass characterClass = name switch
            {
                "alpha" => CharacterClass.Alpha,
                "digit" => CharacterClass.Digit,
                "alnum" => CharacterClass.Alnum,
                "upper" => CharacterClass.Upper,
                "lower" => CharacterClass.Lower,
                "space" => CharacterClass.Space,
                _ => throw new Failure("unknown character class " + name),
            };

            return BracketToken.ForClass(characterClass);
        }

        private bool TryConsume(int value)
        {
            if (position >= source.Length || CurrentCodePoint() != value)
            {
                return false;
            }

            Advance();
            return true;
        }

        private bool NextCodePointIs(int value)
        {
            if (position >= source.Length)
            {
                return false;
            }

            int nextPosition = position + CurrentCodePointLength();
            return nextPosition < source.Length && CodePointAt(source, nextPosition, out _) == value;
        }

        private int CurrentCodePoint() => CodePointAt(source, position, out _);

        private int CurrentCodePointLength() => CodePointLengthAt(source, position);

        private void Advance() => position += CurrentCodePointLength();

        private static bool IsLetterOrDigit(int value)
        {
            if (value <= char.MaxValue)
            {
                return char.IsLetterOrDigit((char)value);
            }

            return Rune.IsLetterOrDigit(new Rune(value));
        }
    }

    private sealed record BracketToken(int? Character, CharacterClass? Class)
    {
        internal static BracketToken ForCharacter(int value) => new(value, null);

        internal static BracketToken ForClass(CharacterClass value) => new(null, value);
    }
}

