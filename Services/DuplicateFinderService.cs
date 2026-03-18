using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class DuplicateFinderService
    {
        public enum KeepStrategy { KeepOldest, KeepNewest, KeepFirst }

        public async Task<List<List<DuplicateFile>>> FindDuplicatesAsync(
            string searchPath,
            string[] extensionFilter = null,
            long minSizeBytes = 4096,
            IProgress<(int scanned, int hashing, string current)> progress = null,
            CancellationToken ct = default)
        {
            return await Task.Run(() =>
                FindDuplicates(searchPath, extensionFilter, minSizeBytes, progress, ct), ct).ConfigureAwait(false);
        }

        private List<List<DuplicateFile>> FindDuplicates(
    string searchPath, string[] extensions, long minSize,
    IProgress<(int scanned, int hashing, string current)> progress, CancellationToken ct)
        {
            var sizeGroups = new Dictionary<long, List<FileInfo>>();
            int scanned = 0;

            var dirInfo = new DirectoryInfo(searchPath);
            foreach (var fi in EnumerateFilesSafe(dirInfo, ct))
            {
                if (ct.IsCancellationRequested) break;
                if (fi.Length < minSize) continue;

                if (extensions?.Length > 0 &&
                    !extensions.Contains(fi.Extension.ToLowerInvariant())) continue;

                if (!sizeGroups.TryGetValue(fi.Length, out var list))
                    sizeGroups[fi.Length] = list = new List<FileInfo>();
                list.Add(fi);

                scanned++;

                if (scanned % 200 == 0)
                    progress?.Report((scanned, 0, fi.DirectoryName ?? ""));
            }

            progress?.Report((scanned, 0, "Grouping by size..."));

            var duplicateGroups = new List<List<DuplicateFile>>();
            int hashing = 0;

            int candidateFiles = sizeGroups.Values.Where(g => g.Count > 1).Sum(g => g.Count);

            foreach (var sizeGroup in sizeGroups.Values.Where(g => g.Count > 1))
            {
                if (ct.IsCancellationRequested) break;

                var quickGroups = new Dictionary<string, List<FileInfo>>();
                foreach (var fi in sizeGroup)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        string quick = ComputeQuickHash(fi.FullName, 4096);
                        if (!quickGroups.TryGetValue(quick, out var qList))
                            quickGroups[quick] = qList = new List<FileInfo>();
                        qList.Add(fi);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log(ex, "FindDuplicates.QuickHash");
                    }
                }

                foreach (var quickGroup in quickGroups.Values.Where(g => g.Count > 1))
                {
                    if (ct.IsCancellationRequested) break;
                    var hashGroups = new Dictionary<string, List<DuplicateFile>>();

                    foreach (var fi in quickGroup)
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            hashing++;
                            progress?.Report((scanned, hashing,
                                $"Hashing {hashing}/{candidateFiles}: {fi.Name}"));

                            // Compute full hash only for files that passed both size and quick hash checks
                            string hash = ComputeFullHash(fi.FullName);
                            if (!hashGroups.TryGetValue(hash, out var hList))
                                hashGroups[hash] = hList = new List<DuplicateFile>();

                            hList.Add(new DuplicateFile
                            {
                                Hash = hash,
                                FileName = fi.Name,
                                FullPath = fi.FullName,
                                Size = fi.Length,
                                LastModified = fi.LastWriteTime
                            });
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            AppLogger.Log(ex, "FindDuplicates.FullHash_Unauthorized");
                        }
                        catch (IOException ex)
                        {
                            AppLogger.Log(ex, "FindDuplicates.FullHash_IO");
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log(ex, "FindDuplicates.FullHash_General");
                        }
                    }
                    foreach (var hGroup in hashGroups.Values.Where(g => g.Count > 1))
                        duplicateGroups.Add(hGroup);
                }
            }
            duplicateGroups.Sort((a, b) =>
                ((long)(b.Count - 1) * b[0].Size).CompareTo((long)(a.Count - 1) * a[0].Size));

            return duplicateGroups;
        }

        public static void ApplyKeepStrategy(
            List<List<DuplicateFile>> groups, KeepStrategy strategy)
        {
            foreach (var group in groups)
            {
                foreach (var f in group) f.IsMarkedForDeletion = false;

                DuplicateFile keep = strategy switch
                {
                    KeepStrategy.KeepNewest => group.OrderByDescending(f => f.LastModified).First(),
                    KeepStrategy.KeepOldest => group.OrderBy(f => f.LastModified).First(),
                    _ => group[0]
                };

                foreach (var f in group)
                    if (f != keep) f.IsMarkedForDeletion = true;
            }
        }

        private static string ComputeQuickHash(string filePath, int bytes)
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(filePath, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite, bytes);
            var buf = new byte[bytes];
            int read = stream.Read(buf, 0, bytes);
            return BitConverter.ToString(md5.ComputeHash(buf, 0, read)).Replace("-", "");
        }

        private static string ComputeFullHash(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(filePath, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite, 131072);
            return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
        }

        private static readonly string[] SkipFolderFragments = new[]
        {
            // System
            "$Recycle.Bin", "System Volume Information",
            "\\Windows\\", "\\Program Files\\", "\\Program Files (x86)\\",
            "\\ProgramData\\", "\\Recovery\\", "\\Boot\\",
            "AppData\\Local\\Temp", "AppData\\LocalLow",

            // Microsoft Edge & WebView2 — intentionally duplicate assets
            "\\Microsoft\\EdgeWebView\\",
            "\\Microsoft\\Edge\\",
            "\\Microsoft\\EdgeUpdate\\",
            "EBWebView",

            // Claude / Anthropic desktop
            "\\Claude\\",
            "\\Anthropic\\",
            "claude-updater",

            // Visual Studio & VS Code
            "\\Microsoft Visual Studio\\",
            "\\.vs\\",
            "\\VSCode\\",
            "\\.vscode\\",
            "\\Extensions\\",

            // Package managers — tons of intentional duplicates
            "\\.nuget\\",
            "\\node_modules\\",
            "\\.cargo\\",
            "\\.gradle\\",
            "\\.m2\\",

            // Git
            "\\.git\\",

            // Browser caches / profiles
            "\\Google\\Chrome\\",
            "\\BraveSoftware\\",
            "\\Mozilla\\Firefox\\",
            "\\Opera Software\\",
        };

        private static readonly HashSet<string> SkipExtensions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".lnk", ".tmp", ".lock", ".log", ".db-wal", ".db-shm",
            ".pf",  ".etl", ".evtx", ".sys", ".dll", ".exe"
        };

        private static IEnumerable<FileInfo> EnumerateFilesSafe(
    DirectoryInfo dir, CancellationToken ct)
        {
            var queue = new Queue<DirectoryInfo>();
            queue.Enqueue(dir);

            while (queue.Count > 0)
            {
                if (ct.IsCancellationRequested) yield break;

                var current = queue.Dequeue();

                if (ShouldSkipDirectory(current.FullName)) continue;

                FileInfo[] files = null;
                try
                {
                    files = current.GetFiles();
                }
                catch (UnauthorizedAccessException ex)
                {
                    AppLogger.Log(ex, "EnumerateFilesSafe.GetFiles_Unauthorized");
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, "EnumerateFilesSafe.GetFiles_General");
                }

                if (files != null)
                {
                    foreach (var f in files)
                    {
                        if (!SkipExtensions.Contains(f.Extension)) yield return f;
                    }
                }

                DirectoryInfo[] dirs = null;
                try
                {
                    dirs = current.GetDirectories();
                }
                catch (UnauthorizedAccessException ex)
                {
                    AppLogger.Log(ex, "EnumerateFilesSafe.GetDirectories_Unauthorized");
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, "EnumerateFilesSafe.GetDirectories_General");
                }

                if (dirs != null)
                {
                    foreach (var d in dirs)
                    {
                        if (!ShouldSkipDirectory(d.FullName)) queue.Enqueue(d);
                    }
                }
            }
        }

        private static bool ShouldSkipDirectory(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return true;

            string name = Path.GetFileName(fullPath);

            if (name.StartsWith("$")) return true;

            foreach (var fragment in SkipFolderFragments)
            {
                if (fullPath.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            string profiles = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string usersRoot = Path.GetDirectoryName(profiles);

            if (!string.IsNullOrEmpty(usersRoot) &&
                fullPath.StartsWith(usersRoot, StringComparison.OrdinalIgnoreCase))
            {
                if (!fullPath.StartsWith(profiles, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            try
            {
                var attr = File.GetAttributes(fullPath);
                if (attr.HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "ShouldSkipDirectory.GetAttributes");
                return true;
            }

            return false;
        }
    }
}