# NetDumper

Game data download script for preserving Final Fantasy VII Ever Crisis ahead of end of service.
Ported from [`NetDumper`](https://github.com/shalzuth/ff7ecapi/blob/main/NetDumper/Dumper.cs) in ff7ecapi.

It downloads assets over the web using the same in-app content upgrade process, so no new APK is needed:

1. `GetCheck` call to obtain the current master data base URL and paths.
2. Master data tables (raw `.bin` plus decoded `.json`).
3. English translations.
4. Octo asset bundles and resources.

Existing files are skipped when their hash or size is unchanged, so reruns only fetch what changed.

Scope: read-only download of publicly served game files. No payment bypass, cheating or in-game automation.

## Build and run

Needs the .NET 8 SDK and a checkout of [ff7ecapi](https://github.com/shalzuth/ff7ecapi) (default location `../../ff7ecapi`).

```
dotnet run -p:Ff7ecApiRoot=/path/to/ff7ecapi -- --out data
```

Options: `--out <dir>`, `--octo-revision <id>` (global `123173179`, JP `123173171`),
`--skip-masterdata`, `--skip-translations`, `--skip-octo`.

## Changes from upstream

- Cross-platform paths (upstream used Windows `\`), configurable output directory and octo revision.
- Table hash comparison no longer throws on tables that are new since the last run.
- Asset names from the server are checked so they cannot write outside the output directory.
- Failed requests are retried; tables without a MessagePack class are reported instead of silently skipped.
- Removed dead commented-out image dump code.

## Status

Not yet built or run against the live servers. Endpoint URLs, keys and the octo revision come from ff7ecapi and may be stale for the latest app version; refresh them from the current APK.
