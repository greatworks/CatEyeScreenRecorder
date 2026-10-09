using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FreeWindowsScreenRecorder;

internal static class UpdateTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    private static string Fixture(string tag, string asset)
    {
        return "{\"tag_name\":\"" + tag + "\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://github.com/example/cateye/releases/tag/" + tag + "\",\"assets\":[{\"name\":\"" + asset + "\",\"browser_download_url\":\"https://github.com/example/cateye/releases/download/" + tag + "/" + asset + "\"}]}";
    }
    private static int Main(string[] args)
    {
        // A child process reports exactly what Windows passed through argv.
        if (args.Length > 0 && args[0] == "--argv") { File.WriteAllLines(args[1], args); return 0; }
        try { Run(args).GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static async Task Run(string[] args)
    {
        string output = args[0]; Directory.CreateDirectory(output);
        string cache = Path.Combine(output, "cache.txt");
        string valid = Fixture("v2.3.1", "CatEyeScreenRecorder-v2.3.1-Windows-x64.zip");
        string exeOnly = Fixture("v2.3.1", "CatEyeScreenRecorder-Setup-v2.3.1-Windows-x64.exe");
        int requests = 0;
        Func<Uri, Task<string>> success = delegate(Uri uri) { requests++; return Task.FromResult(valid); };
        Func<Uri, Task<string>> failed = delegate(Uri uri) { throw new IOException("Simulated offline connection"); };
        try { await UpdateChecker.CheckLatestCoreAsync("example/cateye", new Version(2, 2, 0), false, cache, failed); throw new Exception("Expected failure"); }
        catch (IOException) { }
        Check(!File.Exists(cache), "Network failure does not create a 24-hour cooldown");
        ReleaseInfo release = await UpdateChecker.CheckLatestCoreAsync("example/cateye", new Version(2, 2, 0), false, cache, success);
        Check(release != null && File.Exists(cache) && requests == 1, "Successful retry detects update and caches success");
        release = await UpdateChecker.CheckLatestCoreAsync("example/cateye", new Version(2, 2, 0), false, cache, success);
        Check(release == null && requests == 1, "Automatic successful checks are throttled");
        release = await UpdateChecker.CheckLatestCoreAsync("example/cateye", new Version(2, 2, 0), true, cache, success);
        Check(release != null && requests == 2, "Manual update check bypasses cooldown");
        Check(UpdateChecker.CachePath("example/cateye", new Version(2, 2, 0)) != UpdateChecker.CachePath("example/cateye", new Version(2, 3, 1)) &&
              UpdateChecker.CachePath("example/cateye", new Version(2, 2, 0)) != UpdateChecker.CachePath("other/cateye", new Version(2, 2, 0)), "Cache is scoped to repository and app version");
        File.WriteAllText(cache, DateTime.UtcNow.AddDays(3).ToString("o"));
        Check(UpdateChecker.ShouldCheck(cache, DateTime.UtcNow), "Future timestamp cannot indefinitely disable checks");
        release = UpdateChecker.ParseReleaseForTest(exeOnly);
        Check(release != null && release.AssetUrl == null, "EXE-only release survives parsing for an explanatory notification");
        foreach (string tag in new string[] { "V2.0.0", "v2.2", "2.2.0.0" })
        {
            release = await UpdateChecker.CheckLatestCoreAsync("example/cateye", new Version(2, 2, 0), true, cache,
                delegate(Uri uri) { return Task.FromResult(Fixture(tag, "CatEye-Windows-x64.zip")); });
            Check(release == null, "Equal/older release does not upgrade: " + tag);
        }
        foreach (string asset in new string[] { "CatEye-source.zip", "CatEye-arm64.zip", "CatEye-x86.zip" })
            Check(UpdateChecker.ParseReleaseForTest(Fixture("v9.0.0", asset)).AssetUrl == null, "Ignores unsuitable asset: " + asset);
        Check(!UpdateChecker.IsGithubUrl(new Uri("https://notgithub.com/malware.zip")), "GitHub hostname validation rejects lookalike domains");
        Check(UpdateChecker.ParseReleaseForTest(Fixture("windows", "CatEye.zip")) == null, "Non-version release tag is rejected");
        string invalidCache = Path.Combine(output, "invalid-cache.txt");
        try { await UpdateChecker.CheckLatestCoreAsync("example/cateye", new Version(2, 2, 0), false, invalidCache, delegate(Uri uri) { return Task.FromResult("{}"); }); throw new Exception("Expected parse failure"); }
        catch (InvalidDataException) { }
        Check(!File.Exists(invalidCache), "Invalid release response does not create cooldown");
        CheckArguments(output, args.Length > 1 ? args[1] : null);
        // Exercise the actual WebClient/TLS path, without touching the user's check cache.
        MethodInfo download = typeof(UpdateChecker).GetMethod("DownloadReleaseJsonAsync", BindingFlags.NonPublic | BindingFlags.Static);
        string live = await (Task<string>)download.Invoke(null, new object[] { new Uri("https://api.github.com/repos/greatworks/CatEyeScreenRecorder/releases/latest") });
        File.WriteAllText(Path.Combine(output, "github-latest.json"), live);
        release = UpdateChecker.ParseReleaseForTest(live);
        Check(release != null, "Live GitHub API succeeds with the app's .NET/TLS transport");
        Console.WriteLine("GitHub latest: " + release.TagName + "; ZIP=" + (release.AssetName ?? "missing"));
    }
    private static void CheckArguments(string output, string oldExe)
    {
        string path = "D:\\CatEye 中文 path\\";
        string child = Path.Combine(output, "new-argv.txt");
        string receiver = Assembly.GetExecutingAssembly().Location;
        string tail = " --target " + FfmpegWriter.Quote(path) + " --restart " + FfmpegWriter.Quote(path + "CatEyeScreenRecorder.exe");
        using (Process p = Process.Start(new ProcessStartInfo(receiver, "--argv " + FfmpegWriter.Quote(child) + tail) { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit();
        string[] values = File.ReadAllLines(child);
        Check(values.Length == 6 && values[2] == "--target" && values[3] == path && values[4] == "--restart", "Updater argv preserves paths with spaces and a trailing slash");
        if (!String.IsNullOrEmpty(oldExe))
        {
            Assembly old = Assembly.LoadFile(Path.GetFullPath(oldExe));
            MethodInfo quote = old.GetType("FreeWindowsScreenRecorder.UpdateChecker").GetMethod("Quote", BindingFlags.NonPublic | BindingFlags.Static);
            string oldQuoted = (string)quote.Invoke(null, new object[] { path });
            child = Path.Combine(output, "old-argv.txt");
            using (Process p = Process.Start(new ProcessStartInfo(receiver, "--argv " + FfmpegWriter.Quote(child) + " --target " + oldQuoted + " --restart " + FfmpegWriter.Quote(path + "CatEyeScreenRecorder.exe")) { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit();
            values = File.ReadAllLines(child);
            Check(values.Length != 6 || values[3] != path || values[4] != "--restart", "Reproduced old updater argv bug with the real old binary's quoting method");
        }
    }
}
