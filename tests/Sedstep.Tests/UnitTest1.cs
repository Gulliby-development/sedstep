using System.Diagnostics;
using System.Text;

namespace Sedstep.Tests;

public class UnitTest1
{
    [Fact]
    public void RunWithoutArgumentsReportsMissingCommand()
    {
        AssertCliFailure(
            [],
            "sedstep: missing command (run, trace, explain or check)\n");
    }

    [Fact]
    public void SingleHelpArgumentPrintsLiteralUsage()
    {
        FakeIo io = new();

        int exitCode = Cli.Run(["--help"], io);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            new[]
            {
                "usage: sedstep run [-n] SCRIPT TEXT\n" +
                "       sedstep trace [-n] SCRIPT TEXT\n" +
                "       sedstep explain SCRIPT\n" +
                "       sedstep check SCRIPT\n",
            },
            io.Outputs);
        Assert.Empty(io.Errors);
        Assert.Equal(0, io.ReadCount);
    }

    [Fact]
    public void UnknownCommandReportsTheCommand()
    {
        AssertCliFailure(
            ["rum", "p", "x"],
            "sedstep: unknown command \"rum\"\n");
    }

    [Fact]
    public void RunReportsUnknownOption()
    {
        AssertCliFailure(
            ["run", "-x", "p", "a"],
            "sedstep: unknown option \"-x\"\n");
    }

    [Fact]
    public void RunWithoutScriptReportsMissingScript()
    {
        AssertCliFailure(
            ["run"],
            "sedstep: missing script\n");
    }

    [Fact]
    public void RunWithoutTextReportsMissingText()
    {
        AssertCliFailure(
            ["run", "p"],
            "sedstep: missing text\n");
    }

    [Fact]
    public void CheckRejectsTooManyArguments()
    {
        AssertCliFailure(
            ["check", "p", "p"],
            "sedstep: too many arguments\n");
    }

    [Fact]
    public void RunRejectsTwoStandardInputArguments()
    {
        AssertCliFailure(
            ["run", "-", "-"],
            "sedstep: only one argument can be -\n");
    }

    [Fact]
    public void CheckReportsDelegatedParserFailure()
    {
        AssertCliFailure(
            ["check", "k"],
            "sedstep: unknown command \"k\" at line 1 column 1\n");
    }

    [Fact]
    public void CheckReportsDelegatedSuccess()
    {
        FakeIo io = new();

        int exitCode = Cli.Run(["check", "p"], io);

        Assert.Equal(0, exitCode);
        Assert.Equal(new[] { "ok, 1 command\n" }, io.Outputs);
        Assert.Empty(io.Errors);
        Assert.Equal(0, io.ReadCount);
    }

    [Fact]
    public async Task EntryPointRejectsMalformedUtf8OnStandardInput()
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("sedstep.dll");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("p");
        startInfo.ArgumentList.Add("-");

        using Process process = new() { StartInfo = startInfo };
        Assert.True(process.Start());

        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        Task outputDrain = process.StandardOutput.BaseStream.CopyToAsync(standardOutput);
        Task errorDrain = process.StandardError.BaseStream.CopyToAsync(standardError);

        byte[] malformedUtf8 = { 0x61, 0xFF, 0x0A };
        await process.StandardInput.BaseStream.WriteAsync(malformedUtf8);
        await process.StandardInput.BaseStream.FlushAsync();
        process.StandardInput.Close();

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("The malformed UTF-8 process test exceeded 30 seconds.");
        }

        await Task.WhenAll(outputDrain, errorDrain);

        // Exact raw bytes catch BOM/newline translation that Console.WriteLine would introduce.
        Assert.Equal(2, process.ExitCode);
        Assert.Equal(Array.Empty<byte>(), standardOutput.ToArray());
        Assert.Equal(
            Encoding.UTF8.GetBytes("sedstep: <stdin>: input is not valid UTF-8\n"),
            standardError.ToArray());
    }

    private static void AssertCliFailure(string[] argv, string expectedError)
    {
        FakeIo io = new();

        int exitCode = Cli.Run(argv, io);

        Assert.Equal(2, exitCode);
        Assert.Empty(io.Outputs);
        Assert.Equal(new[] { expectedError }, io.Errors);
        Assert.Equal(0, io.ReadCount);
    }

    private sealed class FakeIo : IIo
    {
        public int ReadCount { get; private set; }

        public List<string> Outputs { get; } = [];

        public List<string> Errors { get; } = [];

        public byte[] ReadStdin()
        {
            ReadCount++;
            return [];
        }

        public void Out(string text)
        {
            Outputs.Add(text);
        }

        public void Err(string text)
        {
            Errors.Add(text);
        }
    }
}
