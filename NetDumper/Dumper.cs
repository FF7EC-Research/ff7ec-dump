using Google.Protobuf;
using MessagePack;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utils;

namespace NetDumper
{
    public static class Dumper
    {
        public const string DefaultApiServerDomain = "https://game-q74z3cyn.app.gl.ffviiec.com";
        public const string DefaultApiServerDomainJp = "https://game-d92afpwh.app.jp.ffviiec.com";
        public const string DefaultWebviewDomain = "https://webview-w62j4u3y.app.gl.ffviiec.com";
        public static string resourcesUrl = "https://resources-api-c9ps53g2.app.gl.ffviiec.com/v1/";
        // octo revision: 123173179 = global, 123173171 = jp. Override with --octo-revision.
        public static string octoRevision = "123173179";
        public static string dumpPath = "data";
        static readonly Dictionary<string, string> assetHeaders = new Dictionary<string, string> {
            { "Accept", "*/*" },
            { "Accept-Encoding", "gzip, deflate, br" },
            { "User-Agent", "FF7EC.47/25 CFNetwork/1410.0.3 Darwin/22.6.0" },
        };
        static HttpRequest http = new HttpRequest { };
        static JsonFormatter prettyJson = new JsonFormatter(JsonFormatter.Settings.Default.WithIndentation());
        static JsonSerializerOptions prettyJsonNet = new JsonSerializerOptions { WriteIndented = true };

        static string P(params string[] parts) => Path.Combine(new[] { dumpPath }.Concat(parts).ToArray());
        // Asset names come from the server; never let them escape the dump directory.
        static string SafeP(string root, string name)
        {
            var full = Path.GetFullPath(Path.Combine(P(root), name));
            if (!full.StartsWith(Path.GetFullPath(P(root)) + Path.DirectorySeparatorChar)) throw new InvalidOperationException("unsafe path: " + name);
            return full;
        }
        static string TableName(string name, bool requirePrefix) =>
            !requirePrefix || name.StartsWith("m_")
                ? string.Join("", name.Split('_').Skip(requirePrefix ? 1 : 0).Where(s => s.Length > 0).Select(s => char.ToUpper(s[0]) + s.Substring(1)))
                : name;
        static async Task<byte[]> Fetch(string url, Dictionary<string, string>? headers = null)
        {
            for (var attempt = 1; ; attempt++)
            {
                try { return await http.GetAsync(url, headers); }
                catch (HttpRequestException) when (attempt < 4) { await Task.Delay(2000 * attempt); }
            }
        }

        public static async Task DownloadTranslations()
        {
            var oldFiles = new Dictionary<string, string> { };
            var indexPath = P("masterData", "masterLanguage.json");
            if (File.Exists(indexPath))
            {
                var oldData = JsonNode.Parse(File.ReadAllText(indexPath))!;
                oldFiles = oldData["files"]!.AsArray().ToDictionary(f => f!["name"]!.ToString(), f => f!["hash"]!.ToString());
            }
            var data = JsonNode.Parse(Encoding.UTF8.GetString(Crypto.DecryptFile(await Fetch(HttpRequest.MasterBaseUrl + HttpRequest.MasterLanguagePath))))!;

            Directory.CreateDirectory(P("masterData", "en"));
            File.WriteAllText(indexPath, data.ToJsonString(prettyJsonNet));
            foreach (var file in data["files"]!.AsArray())
            {
                var name = file!["name"]!.ToString();
                var outFile = P("masterData", "en", TableName(name, true) + ".json");
                if (File.Exists(outFile) && oldFiles.TryGetValue(name, out var oldHash) && file["hash"]!.ToString() == oldHash) continue;
                var d = JsonNode.Parse(Encoding.UTF8.GetString(Compresso.DecompressGz(Crypto.DecryptFile(await Fetch(HttpRequest.MasterBaseUrl + file["path"])))))!;
                File.WriteAllText(outFile, d.ToJsonString(prettyJsonNet));
            }
        }

        public static async Task DownloadMasterData()
        {
            var oldFiles = new Dictionary<string, string> { };
            var indexPath = P("masterData", "masterData.json");
            if (File.Exists(indexPath))
            {
                var oldData = JsonNode.Parse(File.ReadAllText(indexPath))!;
                oldFiles = oldData["tables"]!.AsArray().ToDictionary(f => f!["table"]!.ToString(), f => f!["hash"]!.ToString());
            }
            var data = JsonNode.Parse(Encoding.UTF8.GetString(Crypto.DecryptFile(await Fetch(HttpRequest.MasterBaseUrl + HttpRequest.MasterPath))))!;
            Directory.CreateDirectory(P("masterData", "raw"));
            Directory.CreateDirectory(P("masterData", "tables"));
            File.WriteAllText(indexPath, data.ToJsonString(prettyJsonNet));
            foreach (var file in data["tables"]!.AsArray())
            {
                var table = file!["table"]!.ToString();
                var tableName = TableName(table, true);
                var outFile = P("masterData", "tables", tableName + ".json");
                if (File.Exists(outFile) && oldFiles.TryGetValue(table, out var oldHash) && file["hash"]!.ToString() == oldHash) continue;
                var magic = new byte[] { 0x04, 0x22, 0x4D, 0x18, 0x60, 0x40, 0x82 };
                var encrypted = await Fetch(HttpRequest.MasterBaseUrl + file["path"]);
                File.WriteAllBytes(P("masterData", "raw", tableName + ".bin"), encrypted);
                var decrypted = Crypto.DecryptFile(encrypted);
                if (!decrypted.Take(magic.Length).SequenceEqual(magic)) Console.WriteLine("BAD MAGIC FOR " + tableName);
                var decompressed = Compresso.DecompressLz(decrypted);
                var type = typeof(Command.DB.Accessory).Assembly.GetType("Command.DB." + tableName);
                if (type == null)
                {
                    Console.WriteLine("no MessagePack class for " + tableName + " (raw .bin saved only)");
                    continue;
                }
                type = typeof(List<>).MakeGenericType(type);
                var obj = MessagePackSerializer.Deserialize(type, decompressed);
                File.WriteAllText(outFile, JsonSerializer.Serialize(obj, prettyJsonNet));
            }
        }

        public static async Task DumpOcto()
        {
            // revision lookup is kept so a bad/old revision id is noticed early
            var revision = Octo.Proto.Database.Parser.ParseFrom(await http.GetResourceAsync(resourcesUrl + "revision/" + octoRevision));
            Console.WriteLine("octo revision " + revision.Revision);
            var octocacheevai = Octo.Proto.Database.Parser.ParseFrom(await http.GetResourceAsync(resourcesUrl + "list/" + octoRevision + "/0")); // last thing filters more the higher you go
            Directory.CreateDirectory(P("octo"));
            File.WriteAllBytes(P("octo", "octocacheevai.bin"), octocacheevai.ToByteArray());
            File.WriteAllText(P("octo", "octocacheevai.json"), prettyJson.Format(octocacheevai));
            foreach (var data in octocacheevai.AssetBundleList)
            {
                if (data.Name == "environment/common/skybox.d") continue;
                var outFile = SafeP("octo", data.Name);
                // size match is treated as up to date
                if (File.Exists(outFile) && data.Size == new FileInfo(outFile).Length) continue;
                var assetPayload = await Fetch(octocacheevai.UrlFormat.Replace("{o}", data.ObjectName), assetHeaders);
                assetPayload = Xor.StaticFileCipher(assetPayload, data.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
                File.WriteAllBytes(outFile, assetPayload);
            }
            foreach (var data in octocacheevai.ResourceList)
            {
                var outFile = SafeP(Path.Combine("octo", "resources"), data.Name);
                if (File.Exists(outFile)) continue;
                var file = await Fetch(octocacheevai.UrlFormat.Replace("{o}", data.ObjectName), assetHeaders);
                Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
                File.WriteAllBytes(outFile, file);
            }
        }
    }
}
