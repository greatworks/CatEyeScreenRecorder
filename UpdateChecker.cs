using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace FreeWindowsScreenRecorder
{
    internal sealed class ReleaseInfo
    {
        internal Version Version;
        internal string TagName;
        internal string HtmlUrl;
        internal string AssetName;
        internal string AssetUrl;
        internal string Digest;
        internal string Notes;
    }

    [DataContract]
    public sealed class GithubReleasePayload
    {
        [DataMember(Name = "tag_name")] public string TagName { get; set; }
        [DataMember(Name = "html_url")] public string HtmlUrl { get; set; }
        [DataMember(Name = "body")] public string Body { get; set; }
        [DataMember(Name = "draft")] public bool Draft { get; set; }
        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        [DataMember(Name = "assets")] public GithubReleaseAsset[] Assets { get; set; }
    }

    [DataContract]
    public sealed class GithubReleaseAsset
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "browser_download_url")] public string BrowserDownloadUrl { get; set; }
        [DataMember(Name = "digest")] public string Digest { get; set; }
    }

    internal static class UpdateChecker
    {
        // The release repository is supplied in update.config beside the executable.
        internal const string CurrentVersionText = "2.1.1";
        private static readonly Version CurrentVersion = new Version(CurrentVersionText);
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.config");
        private static readonly string StatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameboxRecorder", "update-check.txt");

        internal static string Repository { get { return ReadConfig("repository"); } }
        internal static bool Enabled { get { return IsRepository(Repository); } }

        internal static async Task<ReleaseInfo> CheckLatestAsync()
        {
            string repository = Repository;
            if (!IsRepository(repository) || !ShouldCheck()) return null;
            MarkChecked();
            string endpoint = "https://api.github.com/repos/" + repository + "/releases/latest";
            using (WebClient client = CreateClient())
            {
                string json = await client.DownloadStringTaskAsync(new Uri(endpoint)).ConfigureAwait(false);
                ReleaseInfo release = ParseRelease(json);
                return release != null && release.Version > CurrentVersion ? release : null;
            }
        }

        internal static async Task<string> DownloadAsync(ReleaseInfo release)
        {
            if (release == null || String.IsNullOrEmpty(release.AssetUrl)) throw new InvalidOperationException("The update package is missing.");
            Uri uri = new Uri(release.AssetUrl);
            if (uri.Scheme != Uri.UriSchemeHttps || !uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The update package URL is not a trusted HTTPS GitHub URL.");
            string safeVersion = release.Version.ToString().Replace('.', '_');
            string package = Path.Combine(Path.GetTempPath(), "CatEyeScreenRecorder-update-" + safeVersion + ".zip");
            using (WebClient client = CreateClient()) await client.DownloadFileTaskAsync(uri, package).ConfigureAwait(false);
            VerifyDigest(package, release.Digest);
            return package;
        }

        internal static void LaunchInstaller(ReleaseInfo release, string package)
        {
            string installer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CatEyeUpdater.exe");
            if (!File.Exists(installer)) throw new FileNotFoundException("The update helper is missing.", installer);
            string args = "--pid " + Process.GetCurrentProcess().Id +
                " --package " + Quote(package) +
                " --target " + Quote(AppDomain.CurrentDomain.BaseDirectory) +
                " --restart " + Quote(ApplicationPath());
            Process.Start(new ProcessStartInfo(installer, args) { WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory, UseShellExecute = false });
        }

        // Kept internal so the offline test suite can verify GitHub response parsing without network access.
        internal static ReleaseInfo ParseReleaseForTest(string json) { return ParseRelease(json); }

        private static ReleaseInfo ParseRelease(string json)
        {
            try
            {
                GithubReleasePayload root;
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    root = (GithubReleasePayload)new DataContractJsonSerializer(typeof(GithubReleasePayload)).ReadObject(stream);
                if (root == null || root.Draft || root.Prerelease) return null;
                string tag = root.TagName; Version version = ParseVersion(tag);
                if (version == null) return null;
                ReleaseInfo result = new ReleaseInfo { Version = version, TagName = tag, HtmlUrl = root.HtmlUrl, Notes = root.Body };
                if (root.Assets == null) return null;
                foreach (GithubReleaseAsset asset in root.Assets)
                {
                    if (asset == null) continue;
                    string name = asset.Name;
                    if (String.IsNullOrEmpty(name) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.IndexOf("CatEye", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("ScreenRecorder", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    result.AssetName = name; result.AssetUrl = asset.BrowserDownloadUrl; result.Digest = asset.Digest; break;
                }
                return String.IsNullOrEmpty(result.AssetUrl) ? null : result;
            }
            catch (Exception) { return null; }
        }

        private static WebClient CreateClient()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "CatEyeScreenRecorder/" + CurrentVersionText;
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            client.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            return client;
        }
        private static Version ParseVersion(string tag)
        {
            if (String.IsNullOrEmpty(tag)) return null;
            string text = tag.Trim(); if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text.Substring(1);
            int dash = text.IndexOf('-'); if (dash >= 0) text = text.Substring(0, dash);
            Version version; return Version.TryParse(text, out version) ? version : null;
        }
        private static bool ShouldCheck()
        {
            try
            {
                if (!File.Exists(StatePath)) return true;
                DateTime last; if (!DateTime.TryParse(File.ReadAllText(StatePath), null, System.Globalization.DateTimeStyles.RoundtripKind, out last)) return true;
                return DateTime.UtcNow - last.ToUniversalTime() >= TimeSpan.FromHours(24);
            }
            catch (Exception) { return true; }
        }
        private static void MarkChecked()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(StatePath)); File.WriteAllText(StatePath, DateTime.UtcNow.ToString("o")); } catch (Exception) { }
        }
        private static string ReadConfig(string key)
        {
            try
            {
                if (!File.Exists(ConfigPath)) return String.Empty;
                foreach (string raw in File.ReadAllLines(ConfigPath))
                {
                    string line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#")) continue;
                    int split = line.IndexOf('='); if (split < 0) continue;
                    if (String.Equals(line.Substring(0, split).Trim(), key, StringComparison.OrdinalIgnoreCase)) return line.Substring(split + 1).Trim();
                }
            }
            catch (Exception) { }
            return String.Empty;
        }
        private static bool IsRepository(string repository)
        {
            if (String.IsNullOrEmpty(repository)) return false;
            string[] parts = repository.Split('/'); return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 && repository.IndexOf("..", StringComparison.Ordinal) < 0;
        }
        private static void VerifyDigest(string path, string digest)
        {
            if (String.IsNullOrEmpty(digest)) return;
            string expected = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest.Substring(7) : digest;
            using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The downloaded update failed its SHA-256 check.");
            }
        }
        private static string Quote(string value) { return "\"" + (value ?? String.Empty).Replace("\"", "\\\"") + "\""; }
        private static string ApplicationPath() { return Process.GetCurrentProcess().MainModule.FileName; }
    }
}
