namespace Sedstep;

public sealed record Parsed(bool Quiet, IReadOnlyList<string> Args);

public static class Options
{
    public static Parsed Parse(IReadOnlyList<string> argv, bool allowQuiet)
    {
        bool quiet = false;
        List<string> args = [];
        bool optionsEnded = false;

        foreach (string argument in argv)
        {
            if (optionsEnded)
            {
                args.Add(argument);
                continue;
            }

            if (argument == "--")
            {
                optionsEnded = true;
                continue;
            }

            if (argument.Length > 1 && argument[0] == '-')
            {
                if (allowQuiet && argument == "-n")
                {
                    quiet = true;
                    continue;
                }

                throw new Failure("unknown option " + Text.Show(argument));
            }

            args.Add(argument);
        }

        return new Parsed(quiet, args);
    }
}
