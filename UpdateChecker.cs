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
        internal const string CurrentVersionText = "2.3.1";
        private static readonly Version CurrentVersion = new Version(CurrentVersionText);
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.config");
        private static readonly string StateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameboxRecorder");

        internal static string Repository { get { return ReadConfig("repository"); } }
        internal static bool Enabled { get { return IsRepository(Repository); } }

        internal static Task<ReleaseInfo> CheckLatestAsync(bool force = false)
        {
            string repository = Repository;
            return CheckLatestCoreAsync(repository, CurrentVersion, force, CachePath(repository, CurrentVersion), DownloadReleaseJsonAsync);
        }

        internal static async Task<ReleaseInfo> CheckLatestCoreAsync(string repository, Version current, bool force, string statePath, Func<Uri, Task<string>> download)
        {
            if (!IsRepository(repository)) throw new InvalidOperationException("Invalid update.config repository. Expected OWNER/REPOSITORY.");
            if (!force && !ShouldCheck(statePath, DateTime.UtcNow)) { Log("Automatic check skipped: successful check within 24 hours."); return null; }
            try
            {
                string json = await download(new Uri("https://api.github.com/repos/" + repository + "/releases/latest")).ConfigureAwait(false);
                ReleaseInfo release = ParseRelease(json);
                if (release == null) throw new InvalidDataException("The latest release is not a stable version with a valid tag (for example v" + CurrentVersionText + ").");
                // Failed requests and invalid responses must never consume the daily check.
                MarkChecked(statePath);
                Log("repository=" + repository + "; local=" + current + "; latest=" + release.TagName + "; ZIP=" + (release.AssetName ?? "missing"));
                return NormalizeVersion(release.Version) > NormalizeVersion(current) ? release : null;
            }
            catch (Exception ex) { Log("Update check failed: " + ex.Message); throw; }
        }

        private static async Task<string> DownloadReleaseJsonAsync(Uri endpoint)
        {
            using (WebClient client = CreateClient())
            {
                // JSON is UTF-8. Framework WebClient otherwise falls back to the
                // Windows ANSI code page, which can consume JSON quote bytes after
                // Chinese release notes and make an otherwise valid response fail.
                Task<byte[]> request = client.DownloadDataTaskAsync(endpoint);
                if (await Task.WhenAny(request, Task.Delay(15000)).ConfigureAwait(false) != request)
                {
                    client.CancelAsync();
                    throw new TimeoutException("GitHub update check timed out after 15 seconds.");
                }
                return Encoding.UTF8.GetString(await request.ConfigureAwait(false));
            }
        }

        internal static async Task<string> DownloadAsync(ReleaseInfo release)
        {
            if (release == null || String.IsNullOrEmpty(release.AssetUrl)) throw new InvalidOperationException("The update package is missing.");
            Uri uri = new Uri(release.AssetUrl);
            if (!IsGithubUrl(uri))
                throw new InvalidOperationException("The update package URL is not a trusted HTTPS GitHub URL.");
            string safeVersion = release.Version.ToString().Replace('.', '_');
            string package = Path.Combine(Path.GetTempPath(), "CatEyeScreenRecorder-update-" + safeVersion + "-" + Guid.NewGuid().ToString("N") + ".zip");
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
            Process.Start(new ProcessStartInfo(installer, args) { WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory, UseShellExecute = false, CreateNoWindow = true });
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
                foreach (GithubReleaseAsset asset in root.Assets ?? new GithubReleaseAsset[0])
                {
                    if (asset == null) continue;
                    string name = asset.Name;
                    if (String.IsNullOrEmpty(name) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.IndexOf("CatEye", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("ScreenRecorder", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (name.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("arm64", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("x86", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    Uri assetUri;
                    if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out assetUri) || !IsGithubUrl(assetUri)) continue;
                    result.AssetName = name; result.AssetUrl = asset.BrowserDownloadUrl; result.Digest = asset.Digest; break;
                }
                // A real release without a ZIP still needs an explanatory UI; don't
                // silently reinterpret it as "no update".
                return result;
            }
            catch (Exception) { return null; }
        }

        private static WebClient CreateClient()
        {
            // .NET 4.5.2 applications do not always inherit the OS TLS default.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            WebClient client = new WebClient();
            client.Encoding = Encoding.UTF8;
            client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
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
        internal static bool ShouldCheck(string path, DateTime now)
        {
            try
            {
                if (!File.Exists(path)) return true;
                DateTime last; if (!DateTime.TryParse(File.ReadAllText(path), null, System.Globalization.DateTimeStyles.RoundtripKind, out last)) return true;
                TimeSpan age = now - last.ToUniversalTime();
                return age < TimeSpan.Zero || age >= TimeSpan.FromHours(24);
            }
            catch (Exception) { return true; }
        }
        private static void MarkChecked(string path)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, DateTime.UtcNow.ToString("o")); } catch (Exception) { }
        }
        internal static string CachePath(string repository, Version version)
        {
            using (SHA256 sha = SHA256.Create())
            {
                string key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes((repository ?? "").ToLowerInvariant() + "@" + version))).Replace("-", "").Substring(0, 16);
                return Path.Combine(StateDirectory, "update-check-" + key + ".txt");
            }
        }
        private static Version NormalizeVersion(Version version) { return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision)); }
        internal static bool IsGithubUrl(Uri uri) { return uri != null && uri.Scheme == Uri.UriSchemeHttps && String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase); }
        internal static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(StateDirectory); string path = Path.Combine(StateDirectory, "update.log");
                if (File.Exists(path) && new FileInfo(path).Length > 65536) File.WriteAllText(path, String.Empty);
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine);
            }
            catch (Exception) { }
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
        private static string Quote(string value) { return FfmpegWriter.Quote(value ?? String.Empty); }
        private static string ApplicationPath() { return Process.GetCurrentProcess().MainModule.FileName; }
    }
}
