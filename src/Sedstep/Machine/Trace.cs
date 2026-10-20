namespace Sedstep;

public record Row(int Line, Cmd? Command, string Out, string Pattern, string Hold);

public record Execution(string Output, IReadOnlyList<Row> Rows, int Steps, int Cycles);

internal sealed class TraceCollector
{
    private readonly List<Row>? rows;

    internal TraceCollector(bool enabled)
    {
        rows = enabled ? [] : null;
    }

    internal IReadOnlyList<Row> Rows =>
        rows is null ? Array.Empty<Row>() : rows.AsReadOnly();

    internal int Mark(OutputBuffer output) => rows is null ? 0 : output.Mark();

    internal void RecordCommand(
        int line,
        Cmd command,
        Space pattern,
        Space hold,
        OutputBuffer output,
        int mark)
    {
        if (rows is null)
        {
            return;
        }

        rows.Add(new Row(line, command, output.Since(mark), pattern.Text, hold.Text));
    }

    internal void RecordEnd(
        int line,
        Space pattern,
        Space hold,
        OutputBuffer output,
        int mark)
    {
        if (rows is null)
        {
            return;
        }

        rows.Add(new Row(line, null, output.Since(mark), pattern.Text, hold.Text));
    }
}
