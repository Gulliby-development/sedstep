using System.Globalization;
using System.Text;

namespace Sedstep;

public static class CmdTrace
{
    public static int Run(IReadOnlyList<string> args, IIo io)
    {
        var prepared = CmdRun.Prepare(args, io, allowQuiet: true, takesText: true);
        Execution execution = Machine.Execute(prepared.Commands, prepared.Text!, prepared.Options.Quiet, trace: true);

        List<IReadOnlyList<string>> rows = [];
        foreach (Row row in execution.Rows)
        {
            rows.Add([
                row.Line.ToString(CultureInfo.InvariantCulture),
                row.Command is null ? "-" : row.Command.Number.ToString(CultureInfo.InvariantCulture),
                row.Command is null ? "(end)" : Text.Cell(ScriptText.Of(row.Command)),
                Text.Esc(row.Pattern),
                Text.Esc(row.Hold),
                row.Out.Length == 0 ? "-" : Text.Esc(row.Out),
            ]);
        }

        List<string> lines = Text.Table(["line", "cmd", "command", "pattern", "hold", "out"], rows);
        lines.Add(Text.Line("steps", execution.Steps.ToString(CultureInfo.InvariantCulture)));
        lines.Add(Text.Line("cycles", execution.Cycles.ToString(CultureInfo.InvariantCulture)));

        StringBuilder output = new();
        foreach (string line in lines)
        {
            output.Append(line);
            output.Append('\n');
        }

        io.Out(output.ToString());
        return 0;
    }
}
