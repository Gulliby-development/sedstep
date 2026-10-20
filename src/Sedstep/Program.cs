using System.Text;

namespace Sedstep;

public static class Program
{
    public static int Main(string[] args)
    {
        return Cli.Run(args, new ConsoleIo());
    }

    private sealed class ConsoleIo : IIo
    {
        private static readonly UTF8Encoding Utf8 = new(false);

        public byte[] ReadStdin()
        {
            using var buffer = new MemoryStream();
            Console.OpenStandardInput().CopyTo(buffer);
            return buffer.ToArray();
        }

        public void Out(string text)
        {
            Console.OpenStandardOutput().Write(Utf8.GetBytes(text));
        }

        public void Err(string text)
        {
            Console.OpenStandardError().Write(Utf8.GetBytes(text));
        }
    }
}
