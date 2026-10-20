# rooksh

A terminal chess GUI that is agnostic about engines. Any two UCI engines, any pairing:
engine-vs-engine, human-vs-engine (either colour), human-vs-human, plus analysis mode.

Plain .NET — the renderer is hand-rolled ANSI/VT with no TUI framework, so the published
binary has no UI dependency.

```
┌──────────────────────────┬────────────────────────┐
│ 8  ♜ ♞ ♝ ♛ ♚ ♝ ♞ ♜      │ A / WHITE              │
│ 7  ♟ ♟ ♟ ♟ ♟ ♟ ♟ ♟      │ Zwischenzug     4:32   │
│ 6  · · · · · · · ·      │ d18 +0.38  1.2Mkn/s    │
│ 5  · · · · · · · ·      │ pv: Nf3 Nc6 Bb5 a6     │
│ 4  · · · ♙ · · · ·      │ B / BLACK              │
│ 3  · · · · · · · ·      │ Stockfish       4:47   │
│ 2  ♙ ♙ ♙ ♙ · ♙ ♙ ♙      │ d20 +0.41  2.4Mkn/s    │
│ 1  ♖ ♘ ♗ ♕ ♔ ♗ ♘ ♖      │ MOVES                  │
│    a b c d e f g h      │   1. e4    e5          │
│                         │   2. Nf3   Nc6         │
└──────────────────────────┴────────────────────────┘
```

## Requirements

- .NET SDK 10.0
- One or more UCI engines (Stockfish, Zwischenzug, …). Binaries are not vendored; drop them in
  `engines/` or point the configuration at any path.

## Build and run

```bash
dotnet build rooksh.slnx

# text-only engine match, prints the PGN (the M1 milestone command)
dotnet run --project src/Rooksh -- engine-match \
  --engine-a-path engines/zwischenzug \
  --engine-b-path engines/stockfish \
  --base-minutes 10 --increment-seconds 5

# interactive match per configuration
dotnet run --project src/Rooksh -- play

# analysis against one engine
dotnet run --project src/Rooksh -- analyze --engine-a-path engines/stockfish --depth 24
dotnet run --project src/Rooksh -- analyze "7k/5Q2/6K1/8/8/8/8/8 w - - 0 1" --infinite
```

Single-file publish:

```bash
dotnet publish src/Rooksh/Rooksh.csproj -c Release -r linux-x64 --self-contained false -o out
```

## Configuration

Copy [`rooksh.example.toml`](rooksh.example.toml) to `rooksh.toml` (git-ignored) and edit it.
Every value has exactly **one canonical name** in all three sources:

| TOML | Environment | CLI |
| --- | --- | --- |
| `[engine_a] path` | `ROOKSH_ENGINE_A_PATH` | `--engine-a-path` |
| `[engine_b] path` | `ROOKSH_ENGINE_B_PATH` | `--engine-b-path` |
| `[match] mode` | `ROOKSH_MODE` | `--mode` |
| `[match] time_control` | `ROOKSH_TIME_CONTROL` | `--time-control` |
| `[match] base_minutes` | `ROOKSH_BASE_MINUTES` | `--base-minutes` |
| `[match] increment_seconds` | `ROOKSH_INCREMENT_SECONDS` | `--increment-seconds` |
| `[match] ponder` | `ROOKSH_PONDER` | `--ponder` / `--no-ponder` |
| `[match] human_side` | `ROOKSH_HUMAN_SIDE` | `--human-side` |
| `[match] depth` / `[match] nodes` | `ROOKSH_DEPTH` / `ROOKSH_NODES` | `--depth` / `--nodes` |
| `[ui] ascii` | `ROOKSH_ASCII` | `--ascii` |
| `[ui] multipv` | `ROOKSH_MULTIPV` | `--multipv` |

Precedence: **CLI flag > environment variable > TOML > default**. TOML is parsed by
[`LukasMoeller.Configuration.Toml`](https://www.nuget.org/packages/LukasMoeller.Configuration.Toml).

### Keys during `play`

| Key | Action |
| --- | --- |
| arrows | move the cursor |
| Enter / Space | select a piece, then a destination |
| letters / digits | type a move in SAN (`Nf3`) or UCI (`g1f3`) |
| Enter | submit the typed move |
| `f` | flip the board |
| `q` | resign |
| Esc | clear selection / typed input |

Promotion defaults to a queen; type the move to choose another piece.

## Architecture

```
Rooksh (host)            CLI, configuration, TUI host, composition root
  └── Rooksh.Match       orchestrator, clocks, PGN, ponder negotiation
        ├── Rooksh.Uci   UCI protocol parser + async client (stdio)
        └── Rooksh.Chess chess-rules adapter (the only place a rules library is referenced)
  └── Rooksh.Rendering   ScreenBuffer, AnsiRenderer, InputParser, board/screen composition
```

Key seams:

- **UI ↔ match** is plain events (`MatchEvent`). The match layer has no rendering references and is
  fully testable headlessly.
- **Match ↔ engine** is `IMoveSource` / `IPonderSource`. An engine, a human, or a test stub all look
  the same to the orchestrator, which is symmetric (White/Black, never "the engine").
- **Match ↔ rules** is `IChessGame`. The concrete library is isolated in `Rooksh.Chess`.
- **UCI client ↔ process** is `IEngineProcess`, so the whole protocol path can run against an
  in-process fake that still speaks real stdio line protocol.

### Rules library note

The spec named `ChessLib`. The `ChessLib` NuGet package on nuget.org mangles positions and does not
track side-to-move, so it cannot drive a real game. `Rooksh.Chess` therefore uses
[`Gera.Chess`](https://www.nuget.org/packages/Gera.Chess) (assembly `ChessLibrary`), which provides
SAN and FEN parsing/generation, legal-move generation and automatic endgame detection
(insufficient material, threefold repetition, fifty-move rule) — everything the spec asks the adapter
for. It is referenced in exactly one file (`GeraChessGame.cs`) behind `IChessGame`.

## Testing

```bash
dotnet test rooksh.slnx                            # everything
dotnet test rooksh.slnx --filter "Category!=Contract"  # skip real-engine contract tests
```

- `Rooksh.Uci.Tests` — protocol parser, `go` rendering, client behaviour against a scripted fake.
- `Rooksh.Match.Tests` — rules adapter, clock math, PGN serializer, orchestrator (full
  engine-vs-engine games with fake engines, timeouts, forfeits, ponder).
- `Rooksh.Rendering.Tests` — buffer diffing, ANSI output, input state machine, screen snapshots.
- `Rooksh.Tests` — configuration resolution and CLI parsing.
- `Rooksh.ContractTests` — the same paths against a real Stockfish. Gated by `Category=Contract` and
  skipped when no binary is found (`ROOKSH_STOCKFISH`, `engines/`, or `PATH`).

## Status

Implemented: UCI client (async, offset writer so `stop` is never blocked), engine-vs-engine
orchestration, clock/time-control math, PGN export, FEN start position, analysis mode with MultiPV,
ponder negotiation with `ponderhit`/`stop`, diff-based renderer, keyboard-driven play, config layering.

Not yet wired: mouse click-to-move in the live terminal (the SGR mouse parser and hit-testing are
implemented and tested, but the portable `Console.ReadKey` front-end does not deliver mouse events),
PGN import, takeback, and engine auto-restart. These are the remaining polish items.

## License

MIT — see [LICENSE](LICENSE).
