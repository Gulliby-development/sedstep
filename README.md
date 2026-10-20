<p align="center"><img src="docs/logo.svg" width="96" alt="" /></p>

# sedstep

sedstep is an offline command-line tool for people who write or read `sed` scripts and want to understand what a script does. It runs a documented subset of `sed -E` on text, shows the pattern and hold spaces as commands run, explains commands in plain language, and reports scripts that cannot work as written. It does not read files or make network requests: provide the script and text as arguments or use standard input.

[![CI](https://github.com/Gulliby-development/sedstep/actions/workflows/ci.yml/badge.svg)](https://github.com/Gulliby-development/sedstep/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Install

Install the .NET 10 SDK, then build from the repository root:

```sh
dotnet build
```

The build creates `src/Sedstep/bin/Debug/net10.0/sedstep.dll`. Invoke that assembly with the cross-platform .NET host, for example:

```sh
dotnet ./src/Sedstep/bin/Debug/net10.0/sedstep.dll --help
```

The command examples below use `sedstep` as shorthand for the command. In a local checkout without a `sedstep` command on `PATH`, replace it with `dotnet ./src/Sedstep/bin/Debug/net10.0/sedstep.dll`. The examples use POSIX-shell syntax for supplying standard input; the application itself runs on Windows, macOS, and Linux.

## Usage

```text
usage: sedstep run [-n] SCRIPT TEXT
       sedstep trace [-n] SCRIPT TEXT
       sedstep explain SCRIPT
       sedstep check SCRIPT
```

`SCRIPT` and `TEXT` are literal text arguments, not file names. Use `-` for standard input; only one argument to a command may be `-`. Standard input is decoded as strict UTF-8, with one leading UTF-8 BOM ignored. `--` ends option parsing, and `-n` is available to `run` and `trace` to suppress automatic printing of the pattern space. Input is split on LF; CR is ordinary text. Output is UTF-8 without a BOM and uses LF line endings. As with sed, output from a final input line without a terminating LF need not end in LF.

## Commands

### `run`

`run` executes the script and writes its result. Unless `-n` is supplied, the pattern space is printed automatically at the end of each cycle.

`printf 'one\ntwo\nthree\nfour\n' | sedstep run -n '2,3p' -` prints:

```text
two
three
```

### `trace`

`trace` executes the script and displays a row for each command that runs, including the current pattern space, hold space, and output attributed to that command. It ends with step and cycle totals. Escaped spaces in the table make newlines visible.

`printf 'one\ntwo\nthree\n' | sedstep trace '1!G;h;$!d' -` prints:

```text
line  cmd  command  pattern            hold               out
1     2    [h]      [one]              [one]              -
1     3    [$!d]    []                 [one]              -
2     1    [1!G]    [two\none]         [one]              -
2     2    [h]      [two\none]         [two\none]         -
2     3    [$!d]    []                 [two\none]         -
3     1    [1!G]    [three\ntwo\none]  [two\none]         -
3     2    [h]      [three\ntwo\none]  [three\ntwo\none]  -
3     -    (end)    [three\ntwo\none]  [three\ntwo\none]  [three\ntwo\none\n]
steps       7
cycles      3
```

### `explain`

`explain` prints a canonical command list with a plain-language description of each command.

`sedstep explain ':a;N;$!ba;s/\n/,/2gp;2,4!{p;D};/x/,+1i bye'` prints:

```text
n  command        meaning
1  [:a]           define the label a
2  [N]            append a newline and the next line to the pattern space; with no next line, end the script
3  [$!b a]        except the last line, jump to the label a
4  [s/\n/,/2gp]   replace match number 2 and every later match of [\n] with [,] and print the pattern space if a replacement was made
5  [2,4!{]        outside the range from line 2 to line 4, run the commands up to the matching }
6  [p]            print the pattern space
7  [D]            delete up to the first newline of the pattern space and restart the cycle without reading a line
8  [/x/,+1i bye]  in the range from a line that matches [x] to the next line, print the text [bye] now
commands    8
```

### `check`

`check` reports static findings. It exits successfully with an `ok` summary when there are none, or lists findings and exits with status 1.

`sedstep check 'd;p;:x;s/a/b/'` exits with 1 and prints:

```text
command 2: SC001 unreachable: command 1 always ends the cycle or jumps away
command 3: SC002 label x is never used
2 findings
```

## Supported subset and limitations

The supported commands are `{`, `}`, labels (`:NAME`), branches (`b`, `t`, `T`), `s`, `a`, `i`, `d`, `D`, `p`, `P`, `n`, `N`, `g`, `G`, `h`, `H`, `x`, `=`, `q`, and `Q`. Addresses include line numbers, `$`, regular expressions, ranges, `+N` second addresses, and negation.

Regular expressions are a bounded POSIX extended-expression subset: literals, `.`, `^`, `$`, groups, `*`, `+`, `?`, intervals, bracket expressions, and the documented ASCII character classes. Regular expressions are limited to 100 characters, and a pattern can have at most nine groups. Alternation and backreferences are not supported. This is not a full sed implementation: commands and regular-expression features outside the subset are rejected rather than interpreted.

Execution is bounded: scripts may contain at most 2,000 characters, no more than 100,000 commands may run, and the pattern and hold spaces are each limited to 10,000 characters. The tool processes supplied text in memory; it does not open named input files or provide in-place editing.

## Exit codes

- `0`: command completed successfully (including `check` when no findings were found).
- `1`: `check` found one or more issues.
- `2`: invalid arguments, input, script, or execution limit error.

## Development

From the repository root, run the build, formatting check, and test suite:

```sh
dotnet build
dotnet format --verify-no-changes
dotnet test
```

## License

MIT, see [LICENSE](LICENSE).

---

Built with BAKED
