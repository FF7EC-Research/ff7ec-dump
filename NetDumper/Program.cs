// Downloads FF7EC game data over the web using the same content-upgrade endpoints as the app,
// so no new APK is needed. Read-only: this tool only fetches publicly served game files.
//
// usage: NetDumper [--out <dir>] [--octo-revision <id>] [--skip-masterdata] [--skip-translations] [--skip-octo]
var opts = new Dictionary<string, string>();
var flags = new HashSet<string>();
for (var i = 0; i < args.Length; i++)
{
    if (!args[i].StartsWith("--")) continue;
    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) opts[args[i]] = args[++i];
    else flags.Add(args[i]);
}
if (opts.TryGetValue("--out", out var outDir)) NetDumper.Dumper.dumpPath = outDir;
if (opts.TryGetValue("--octo-revision", out var rev)) NetDumper.Dumper.octoRevision = rev;

// get latest paths (populates master data base url/paths from response headers)
await new ff7ecnet.API(0).GetCheck(new Api.GetCheckRequest { });

if (!flags.Contains("--skip-masterdata")) await NetDumper.Dumper.DownloadMasterData(); // raw game data, like json
if (!flags.Contains("--skip-translations")) await NetDumper.Dumper.DownloadTranslations(); // english
if (!flags.Contains("--skip-octo")) await NetDumper.Dumper.DumpOcto(); // raw game assets, like models, images, etc.
