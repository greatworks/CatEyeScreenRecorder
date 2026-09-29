using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

internal static class UpdateInstaller
{
    private static int Main(string[] args)
    {
        try
        {
            string package = Value(args, "--package"); string target = Value(args, "--target"); string restart = Value(args, "--restart");
            int pid; if (!Int32.TryParse(Value(args, "--pid"), out pid) || String.IsNullOrEmpty(package) || String.IsNullOrEmpty(target) || String.IsNullOrEmpty(restart)) throw new ArgumentException("Invalid update arguments.");
            WaitForParent(pid); string staging = Path.Combine(Path.GetTempPath(), "cateye-update-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging); Extract(package, staging); CopyFiles(staging, target); Process.Start(new ProcessStartInfo(restart) { WorkingDirectory = target });
            }
            finally { TryDelete(staging); TryDelete(package); }
            return 0;
        }
        catch (Exception ex) { try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "CatEyeUpdater-error.log"), ex.ToString()); } catch (Exception) { } return 1; }
    }

    private static void WaitForParent(int pid)
    {
        try { using (Process parent = Process.GetProcessById(pid)) { if (!parent.WaitForExit(120000)) throw new TimeoutException("The recorder did not close in time."); } }
        catch (ArgumentException) { }
    }
    private static void Extract(string package, string staging)
    {
        string root = Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using (ZipArchive archive = ZipFile.OpenRead(package))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(staging, relative));
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe path in update package.");
                if (String.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using (Stream input = entry.Open()) using (FileStream output = File.Create(destination)) input.CopyTo(output);
            }
        }
    }
    private static void CopyFiles(string source, string target)
    {
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(directory.Replace(source, target));
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target); Directory.CreateDirectory(Path.GetDirectoryName(destination));
            Exception last = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try { File.Copy(file, destination, true); last = null; break; } catch (Exception ex) { last = ex; Thread.Sleep(250); }
            }
            if (last != null) throw last;
        }
    }
    private static string Value(string[] args, string key)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (String.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return String.Empty;
    }
    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); } catch (Exception) { } }
}
