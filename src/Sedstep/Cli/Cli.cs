namespace Sedstep;

public static class Cli
{
    public const string Usage =
        "usage: sedstep run [-n] SCRIPT TEXT\n"
        + "       sedstep trace [-n] SCRIPT TEXT\n"
        + "       sedstep explain SCRIPT\n"
        + "       sedstep check SCRIPT\n";

    private static readonly IReadOnlyDictionary<string, Func<IReadOnlyList<string>, IIo, int>> Commands =
        new Dictionary<string, Func<IReadOnlyList<string>, IIo, int>>(StringComparer.Ordinal)
        {
            ["run"] = CmdRun.Run,
            ["trace"] = CmdTrace.Run,
            ["explain"] = CmdExplain.Run,
            ["check"] = CmdCheck.Run,
        };

    public static int Run(string[] argv, IIo io)
    {
        try
        {
            if (argv.Length == 0)
            {
                throw new Failure("missing command (run, trace, explain or check)");
            }

            if (argv.Length == 1 && argv[0] == "--help")
            {
                io.Out(Usage);
                return 0;
            }

            if (!Commands.TryGetValue(argv[0], out Func<IReadOnlyList<string>, IIo, int>? command))
            {
                throw new Failure("unknown command " + Text.Show(argv[0]));
            }

            return command(argv[1..], io);
        }
        catch (Failure failure)
        {
            io.Err("sedstep: " + failure.Message + "\n");
            return 2;
        }
    }
}
