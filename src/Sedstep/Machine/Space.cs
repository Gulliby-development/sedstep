using System.Text;

namespace Sedstep;

internal sealed class Space
{
    private Space(string lengthFailureMessage, string text, bool eol)
    {
        this.lengthFailureMessage = lengthFailureMessage;
        Set(text, eol);
    }

    private readonly string lengthFailureMessage;

    internal static Space Pattern() => new("the pattern space is larger than 10000 characters", "", false);

    internal static Space Hold() => new("the hold space is larger than 10000 characters", "", true);

    internal string Text { get; private set; } = "";

    internal bool Eol { get; private set; }

    internal void Set(string text, bool eol)
    {
        ValidateLength(text);
        Text = text;
        Eol = eol;
    }

    internal void Append(string text, bool eol)
    {
        string combined = string.Concat(Text, text);
        ValidateLength(combined);
        Text = combined;
        Eol = eol;
    }

    internal void Clear()
    {
        Text = "";
        Eol = false;
    }

    internal void CopyFrom(Space other)
    {
        Set(other.Text, other.Eol);
    }

    internal void Swap(Space other)
    {
        (Text, other.Text) = (other.Text, Text);
        (Eol, other.Eol) = (other.Eol, Eol);
    }

    private void ValidateLength(string text)
    {
        int count = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index])
                && index + 1 < text.Length
                && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
            }

            count++;
            if (count > 10000)
            {
                throw new Failure(lengthFailureMessage);
            }
        }
    }
}

internal sealed class OutputBuffer
{
    private readonly StringBuilder output = new();
    private readonly List<string> queued = [];
    private bool pendingLf;

    internal int Mark() => output.Length;

    internal string Text => output.ToString();

    internal string Since(int mark) => output.ToString(mark, output.Length - mark);

    internal void Write(string text)
    {
        BeginWrite();
        output.Append(text);
    }

    internal void WriteLine(string text)
    {
        BeginWrite();
        output.Append(text);
        output.Append('\n');
    }

    internal void Print(Space space)
    {
        BeginWrite();
        output.Append(space.Text);
        if (space.Eol)
        {
            output.Append('\n');
        }
        else
        {
            pendingLf = true;
        }
    }

    internal void PrintFirst(Space space)
    {
        int newline = space.Text.IndexOf('\n');
        if (newline < 0)
        {
            Print(space);
            return;
        }

        BeginWrite();
        output.Append(space.Text.AsSpan(0, newline));
        output.Append('\n');
    }

    internal void Queue(string text) => queued.Add(text);

    internal void FlushPendingLf()
    {
        if (pendingLf)
        {
            output.Append('\n');
            pendingLf = false;
        }
    }

    internal void FlushQueue()
    {
        foreach (string text in queued)
        {
            WriteLine(text);
        }

        queued.Clear();
    }

    private void BeginWrite()
    {
        if (pendingLf)
        {
            output.Append('\n');
            pendingLf = false;
        }
    }
}
