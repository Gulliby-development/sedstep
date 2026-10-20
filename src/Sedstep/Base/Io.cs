using System.Text;

namespace Sedstep;

public interface IIo
{
    byte[] ReadStdin();

    void Out(string text);

    void Err(string text);
}

public static class Input
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Arg(IIo io, string arg)
    {
        if (arg != "-")
        {
            return arg;
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(io.ReadStdin());
        }
        catch (DecoderFallbackException)
        {
            throw new Failure("<stdin>: input is not valid UTF-8");
        }

        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
    }
}
