using System.Globalization;

namespace Sedstep;

public static class CmdCheck
{
    public static int Run(IReadOnlyList<string> args, IIo io)
    {
        var prepared = CmdRun.Prepare(args, io, allowQuiet: false, takesText: false);
        List<Finding> findings = Check.Run(prepared.Commands);
        int commandCount = 0;
        foreach (Cmd command in prepared.Commands)
        {
            if (command.Letter != '}')
            {
                commandCount++;
            }
        }

        List<string> lines = [];
        int exitCode;
        if (findings.Count == 0)
        {
            string count = commandCount.ToString(CultureInfo.InvariantCulture);
            string noun = commandCount == 1 ? "command" : "commands";
            lines.Add("ok, " + count + " " + noun);
            exitCode = 0;
        }
        else
        {
            foreach (Finding finding in findings)
            {
                lines.Add(
                    "command " + finding.Command.ToString(CultureInfo.InvariantCulture)
                    + ": " + finding.Code + " " + finding.Message);
            }

            string count = findings.Count.ToString(CultureInfo.InvariantCulture);
            string noun = findings.Count == 1 ? "finding" : "findings";
            lines.Add(count + " " + noun);
            exitCode = 1;
        }

        io.Out(string.Join("\n", lines) + "\n");
        return exitCode;
    }
}
