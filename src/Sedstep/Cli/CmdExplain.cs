using System.Globalization;

namespace Sedstep;

public static class CmdExplain
{
    public static int Run(IReadOnlyList<string> args, IIo io)
    {
        var prepared = CmdRun.Prepare(args, io, allowQuiet: false, takesText: false);
        List<IReadOnlyList<string>> rows = [];
        int commandCount = 0;

        foreach (Cmd command in prepared.Commands)
        {
            if (command.Letter == '}')
            {
                continue;
            }

            commandCount++;
            rows.Add(new string[]
            {
                command.Number.ToString(CultureInfo.InvariantCulture),
                Text.Cell(ScriptText.Of(command)),
                Explain.Meaning(command),
            });
        }

        List<string> lines = Text.Table(["n", "command", "meaning"], rows);
        lines.Add(Text.Line("commands", commandCount.ToString(CultureInfo.InvariantCulture)));
        io.Out(string.Join("\n", lines) + "\n");
        return 0;
    }
}
