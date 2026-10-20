namespace Sedstep.Tests;

public class TextTests
{
    [Fact]
    public void ShowRendersEmptyText()
    {
        Assert.Equal("\"\"", Text.Show(""));
    }

    [Fact]
    public void ShowPreservesPrintableAsciiBounds()
    {
        Assert.Equal("\"a ~\"", Text.Show("a ~"));
    }

    [Fact]
    public void ShowKeepsLiteralQuotesAndBackslashes()
    {
        Assert.Equal("\"\"\\\"", Text.Show("\"\\"));
    }

    [Fact]
    public void ShowReplacesEachNonPrintableCodePoint()
    {
        Assert.Equal("\"?????\"", Text.Show("é😀\n\t\u007F"));
    }

    [Fact]
    public void CellRendersEmptyText()
    {
        Assert.Equal("[]", Text.Cell(""));
    }

    [Fact]
    public void CellKeepsLiteralBrackets()
    {
        Assert.Equal("[[x]]", Text.Cell("[x]"));
    }

    [Fact]
    public void CellReplacesSupplementaryCodePointOnce()
    {
        Assert.Equal("[?x]", Text.Cell("😀x"));
    }

    [Fact]
    public void EscUsesDefinedBackslashNewlineAndTabEscapes()
    {
        Assert.Equal("[\\\\\\n\\t]", Text.Esc("\\\n\t"));
    }

    [Fact]
    public void EscReplacesOtherNonPrintableCodePoints()
    {
        Assert.Equal("[????]", Text.Esc("é😀\r\u007F"));
    }

    [Fact]
    public void EscKeepsPrintableTextIncludingBrackets()
    {
        Assert.Equal("[a ~[]]", Text.Esc("a ~[]"));
    }

    [Fact]
    public void LinePadsShortKeysToTenCharacters()
    {
        Assert.Equal("steps       7", Text.Line("steps", "7"));
    }

    [Fact]
    public void LineDoesNotTruncateLongKeys()
    {
        Assert.Equal("abcdefghijk  v", Text.Line("abcdefghijk", "v"));
    }

    [Fact]
    public void LineCountsSupplementaryCharactersAsOne()
    {
        Assert.Equal("😀           v", Text.Line("😀", "v"));
    }

    [Fact]
    public void TableAlignsColumnsAcrossHeaderAndRows()
    {
        Assert.Equal(
            new[] { "n   word", "12  x", "3   longer" },
            Text.Table(
                new[] { "n", "word" },
                new IReadOnlyList<string>[]
                {
                    new[] { "12", "x" },
                    new[] { "3", "longer" },
                }));
    }

    [Fact]
    public void TableCanContainOnlyItsHeader()
    {
        Assert.Equal(
            new[] { "name  v" },
            Text.Table(new[] { "name", "v" }, Array.Empty<IReadOnlyList<string>>()));
    }

    [Fact]
    public void TableTrimsTrailingSpacesFromRows()
    {
        Assert.Equal(
            new[] { "a  b", "x" },
            Text.Table(
                new[] { "a", "b" },
                new IReadOnlyList<string>[] { new[] { "x", "" } }));
    }

    [Fact]
    public void TableCountsSupplementaryCharactersAsOneColumnCharacter()
    {
        Assert.Equal(
            new[] { "k  v", "😀  x", "a  y" },
            Text.Table(
                new[] { "k", "v" },
                new IReadOnlyList<string>[]
                {
                    new[] { "😀", "x" },
                    new[] { "a", "y" },
                }));
    }
}
