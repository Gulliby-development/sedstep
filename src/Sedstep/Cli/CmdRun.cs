using System.Globalization;
using System.Text;

namespace Sedstep;

public static class CmdRun
{
    internal static (Parsed Options, IReadOnlyList<Cmd> Commands, string? Text) Prepare(
        IReadOnlyList<string> args,
        IIo io,
        bool allowQuiet,
        bool takesText)
    {
        Parsed options = Options.Parse(args, allowQuiet);
        int requiredArguments = takesText ? 2 : 1;

        if (options.Args.Count == 0)
        {
            throw new Failure("missing script");
        }

        if (takesText && options.Args.Count < 2)
        {
            throw new Failure("missing text");
        }

        if (options.Args.Count > requiredArguments)
        {
            throw new Failure("too many arguments");
        }

        int stdinArguments = 0;
        foreach (string argument in options.Args)
        {
            if (argument == "-")
            {
                stdinArguments++;
            }
        }

        if (stdinArguments > 1)
        {
            throw new Failure("only one argument can be -");
        }

        IReadOnlyList<Cmd> commands = ScriptParser.Parse(Input.Arg(io, options.Args[0]));
        string? text = takesText ? Input.Arg(io, options.Args[1]) : null;
        return (options, commands, text);
    }

    public static int Run(IReadOnlyList<string> args, IIo io)
    {
        var prepared = Prepare(args, io, allowQuiet: true, takesText: true);
        Execution execution = Machine.Execute(prepared.Commands, prepared.Text!, prepared.Options.Quiet, trace: false);
        io.Out(execution.Output);
        return 0;
    }
}
