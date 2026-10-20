using System.Globalization;
using System.Text;

namespace Sedstep;

public static class ScriptText
{
    public static string Of(Cmd command)
    {
        ArgumentNullException.ThrowIfNull(command);

        StringBuilder result = new();
        if (command.First is not null)
        {
            AppendAddress(result, command.First);
            if (command.Second is not null)
            {
                result.Append(',');
                AppendAddress(result, command.Second);
            }
        }

        if (command.Negated)
        {
            result.Append('!');
        }

        switch (command.Letter)
        {
            case '{':
            case '}':
                result.Append(command.Letter);
                break;
            case ':':
                result.Append(':');
                result.Append(command.Label);
                break;
            case 'b':
            case 't':
            case 'T':
                result.Append(command.Letter);
                if (command.Label.Length > 0)
                {
                    result.Append(' ');
                    result.Append(command.Label);
                }

                break;
            case 's':
                result.Append("s/");
                AppendEscapedSlashes(result, command.RegexSource);
                result.Append('/');
                AppendEscapedSlashes(result, command.ReplSource);
                result.Append('/');
                if (command.Count != 0)
                {
                    result.Append(command.Count.ToString(CultureInfo.InvariantCulture));
                }

                if (command.Global)
                {
                    result.Append('g');
                }

                if (command.Print)
                {
                    result.Append('p');
                }

                break;
            case 'a':
            case 'i':
                result.Append(command.Letter);
                result.Append(' ');
                result.Append(command.Text);
                break;
            default:
                result.Append(command.Letter);
                break;
        }

        return result.ToString();
    }

    private static void AppendAddress(StringBuilder result, Addr address)
    {
        switch (address.Kind)
        {
            case AddrKind.Line:
                result.Append(address.A.ToString(CultureInfo.InvariantCulture));
                break;
            case AddrKind.Last:
                result.Append('$');
                break;
            case AddrKind.Regex:
                result.Append('/');
                AppendEscapedSlashes(result, address.Source);
                result.Append('/');
                break;
            case AddrKind.Plus:
                result.Append('+');
                result.Append(address.A.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(address), address.Kind, "Unknown address kind.");
        }
    }

    private static void AppendEscapedSlashes(StringBuilder result, string source)
    {
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            if (current == '\\')
            {
                result.Append(current);
                if (index + 1 < source.Length)
                {
                    result.Append(source[++index]);
                    if (char.IsHighSurrogate(source[index])
                        && index + 1 < source.Length
                        && char.IsLowSurrogate(source[index + 1]))
                    {
                        result.Append(source[++index]);
                    }
                }
            }
            else if (current == '/')
            {
                result.Append("\\/");
            }
            else
            {
                result.Append(current);
            }
        }
    }
}

