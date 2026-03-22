# acc-cli

A command-line interface for querying the [ACC (AdaptiveCodecContext)](https://github.com/KeryxLabs/KeryxInstrumenta) engine — a code graph indexer that surfaces AVEC dimensional metrics (Stability, Logic, Friction, Autonomy) over your codebase.


## Installation

### Download a release
#### Download the pre-built binary for your platform from the latest release:
```
curl -fsSL https://github.com/KeryxLabs/KeryxInstrumenta/releases/download/acc-cli/v{version_number}/acc-cli-linux-x64.tar.gz | tar -xz
```

### Build from source

```bash
git clone https://github.com/KeryxLabs/KeryxInstrumenta
cd src/acc/acc-cli
dotnet build -c Release
```

### Publish as a single binary

```bash
dotnet publish -c Release -r linux-x64    # Linux
dotnet publish -c Release -r osx-arm64    # macOS Apple Silicon
dotnet publish -c Release -r win-x64      # Windows
```
## Requirements 

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- A running ACC engine (default: `localhost:9339`)


The output binary will be at `bin/Release/net10.0/<rid>/publish/acc-cli`.

## Configuration

By default the CLI connects to `localhost:9339`. Override via:

**`appsettings.json`** (next to the binary):
```json
{
  "AccEngine": {
    "Host": "localhost",
    "Port": 9339
  }
}
```

**Environment variables:**
```bash
AccEngine__Host=10.0.0.5
AccEngine__Port=9999
```

**Per-command flags** (highest priority):
```bash
acc-cli --host 10.0.0.5 --port 9999 stats
acc-cli -H 10.0.0.5 -P 9999 stats
```

Priority order: `flag > environment variable > appsettings.json > default`

## Commands

### `stats`
Aggregate health overview of the entire indexed codebase.

```bash
acc-cli stats
```

---

### `lookup get`
Retrieve a single node by its unique ID with full metadata: file location, lines of code,
cyclomatic complexity, git history, test coverage, graph degree, and AVEC scores.

```bash
acc-cli lookup get "<File>:<Symbol>:<LineStart>"

# Example
acc-cli lookup get "UserService.cs:AuthenticateAsync:23"
```

---

### `lookup relations`
Show one-hop incoming and outgoing edges for a node — what it calls and what calls it.

```bash
acc-cli lookup relations "<nodeId>" [--scores|-s]

# Example
acc-cli lookup relations "UserService.cs:AuthenticateAsync:23" --scores
```

| Flag | Description |
|------|-------------|
| `-s`, `--scores` | Include AVEC dimensional scores in the result |

---

### `graph deps`
Traverse transitive dependencies of a node.
- `Incoming` — who depends on this node *(impact analysis: what breaks if I change this?)*
- `Outgoing` — what this node depends on *(dependency analysis: what does this need?)*
- `Both` — full neighbourhood

```bash
acc-cli graph deps "<nodeId>" [options]

# Examples
acc-cli graph deps "UserService.cs:AuthenticateAsync:23"
acc-cli graph deps "UserService.cs:AuthenticateAsync:23" -d Incoming --depth 3
acc-cli graph deps "UserService.cs:AuthenticateAsync:23" -d Outgoing --scores
```

| Flag | Default | Description |
|------|---------|-------------|
| `-d`, `--direction` | `Both` | Traversal direction: `Incoming`, `Outgoing`, or `Both` |
| `--depth` | `-1` | Max traversal depth (`-1` = unlimited) |
| `-s`, `--scores` | `false` | Include AVEC scores in each result node |

---

### `search name`
Case-insensitive substring search for nodes by name.

```bash
acc-cli search name <query> [--limit|-l N]

# Examples
acc-cli search name AuthenticateAsync
acc-cli search name Service -l 20
```

| Flag | Default | Description |
|------|---------|-------------|
| `-l`, `--limit` | `10` | Maximum number of results |

---

### `search patterns`
Find nodes whose AVEC profile is similar to a target using Euclidean distance in 4D space.
Useful for locating architectural patterns — *"show me code structurally similar to X"*.

```bash
acc-cli search patterns <stability> <logic> <friction> <autonomy> [--threshold|-t N]

# Find stable, simple, isolated, independent nodes
acc-cli search patterns 0.9 0.2 0.1 0.9

# Find nodes similar to a known chokepoint profile
acc-cli search patterns 0.5 0.7 0.8 0.3 --threshold 0.85
```

| Argument | Range | Description |
|----------|-------|-------------|
| `stability` | `0.0–1.0` | `0` = high churn, `1` = stable |
| `logic` | `0.0–1.0` | `0` = simple, `1` = complex |
| `friction` | `0.0–1.0` | `0` = isolated, `1` = central chokepoint |
| `autonomy` | `0.0–1.0` | `0` = highly coupled, `1` = independent |

| Flag | Default | Description |
|------|---------|-------------|
| `-t`, `--threshold` | `0.8` | Similarity threshold (`0.0` = any match, `1.0` = near-identical) |

---

### `risk friction`
Find high-friction nodes — central chokepoints that many other nodes depend on.
These are the riskiest nodes to change. Results are ordered by friction score descending.

```bash
acc-cli risk friction [--min|-m N] [--limit|-l N]

# Examples
acc-cli risk friction
acc-cli risk friction --min 0.8 --limit 10
```

| Flag | Default | Description |
|------|---------|-------------|
| `-m`, `--min` | `0.7` | Minimum friction score to include (`0.0–1.0`) |
| `-l`, `--limit` | `20` | Maximum number of results |

---

### `risk unstable`
Find unstable nodes — high-churn, low-coverage code most likely to introduce bugs.
Results are ordered by stability score ascending (least stable first).

```bash
acc-cli risk unstable [--max|-m N] [--limit|-l N]

# Examples
acc-cli risk unstable
acc-cli risk unstable --max 0.3 --limit 5
```

| Flag | Default | Description |
|------|---------|-------------|
| `-m`, `--max` | `0.4` | Maximum stability score to include (`0.0–1.0`) |
| `-l`, `--limit` | `20` | Maximum number of results |

---

## Global Flags

These flags can be placed anywhere in the command and override the configured host/port for that single invocation.

| Flag | Short | Description |
|------|-------|-------------|
| `--host <host>` | `-H` | ACC engine hostname |
| `--port <port>` | `-P` | ACC engine port |

---

## Output

All commands output pretty-printed JSON to stdout, making them easy to pipe into other tools:

```bash
# Pretty-print with jq
acc-cli risk friction | jq '.[0]'

# Extract just node names
acc-cli search name Service | jq '.[].name'

# Save to file
acc-cli graph deps "UserService.cs:AuthenticateAsync:23" -d Incoming > impact.json
```

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | General error (bad arguments, unexpected failure) |
| `2` | ACC engine returned an error response |
| `3` | Could not connect to the ACC engine |