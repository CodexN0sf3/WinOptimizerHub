using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class DiskAnalyzerService
    {
        public List<DiskInfo> GetAllDrives()
        {
            var disks = new List<DiskInfo>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    disks.Add(new DiskInfo
                    {
                        DriveLetter = drive.Name,
                        VolumeLabel = drive.VolumeLabel,
                        DriveType = drive.DriveType.ToString(),
                        FileSystem = drive.DriveFormat,
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.AvailableFreeSpace,
                        IsSSD = DetectSSD(drive.Name)
                    });
                }
                catch (Exception ex) { AppLogger.Log(ex, nameof(GetAllDrives)); }
            }
            return disks;
        }

        public async Task<List<FolderData>> AnalyzeFolderAsync(
            string rootPath, int maxDepth = 2,
            IProgress<string> progress = null, CancellationToken ct = default)
        {
            return await Task.Run(() => AnalyzeFolder(rootPath, maxDepth, progress, ct), ct).ConfigureAwait(false);
        }

        private List<FolderData> AnalyzeFolder(string rootPath, int maxDepth,
            IProgress<string> progress, CancellationToken ct)
        {
            var results = new List<FolderData>();
            try
            {
                var rootDir = new DirectoryInfo(rootPath);
                if (!rootDir.Exists) return results;

                long rootSize = 0;
                foreach (var subDir in rootDir.EnumerateDirectories())
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        progress?.Report($"Analyzing {subDir.Name}...");
                        var (size, files, folders) = GetFolderStats(subDir, maxDepth, 0, ct);
                        rootSize += size;

                        results.Add(new FolderData
                        {
                            Name = subDir.Name,
                            FullPath = subDir.FullName,
                            Size = size,
                            FileCount = files,
                            FolderCount = folders
                        });
                    }
                    catch (Exception ex) { AppLogger.Log(ex, nameof(AnalyzeFolder)); }
                }

                long rootFiles = 0; int fileCountRoot = 0;
                foreach (var file in rootDir.EnumerateFiles())
                {
                    try { rootFiles += file.Length; fileCountRoot++; }
                    catch (Exception ex) { AppLogger.Log(ex, nameof(AnalyzeFolder)); }
                }

                if (fileCountRoot > 0)
                {
                    results.Add(new FolderData
                    {
                        Name = "[Files in root]",
                        FullPath = rootPath,
                        Size = rootFiles,
                        FileCount = fileCountRoot,
                        FolderCount = 0
                    });
                }

                long total = results.Sum(f => f.Size);
                if (total > 0)
                    foreach (var f in results)
                        f.PercentOfParent = f.Size * 100.0 / total;
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(AnalyzeFolder)); }

            return results.OrderByDescending(f => f.Size).ToList();
        }

        private (long size, int files, int folders) GetFolderStats(
            DirectoryInfo dir, int maxDepth, int currentDepth, CancellationToken ct)
        {
            long size = 0; int files = 0; int folders = 0;
            try
            {
                foreach (var file in dir.EnumerateFiles())
                {
                    try { size += file.Length; files++; }
                    catch (Exception ex) { AppLogger.Log(ex, nameof(GetFolderStats)); }
                }

                if (currentDepth < maxDepth)
                {
                    foreach (var subDir in dir.EnumerateDirectories())
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            var (s, f, d) = GetFolderStats(subDir, maxDepth, currentDepth + 1, ct);
                            size += s; files += f; folders += d + 1;
                        }
                        catch (Exception ex) { AppLogger.Log(ex, nameof(GetFolderStats)); }
                    }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(GetFolderStats)); }
            return (size, files, folders);
        }

        private bool DetectSSD(string driveName)
        {
            try
            {
                string driveLetter = driveName.TrimEnd('\\').Replace(":", "");
                string devicePath = $@"\\.\{driveLetter}:";

                using var handle = CreateFile(devicePath,
                    0,
                    FileShare.ReadWrite,
                    IntPtr.Zero,
                    FileMode.Open,
                    0, IntPtr.Zero);

                if (handle.IsInvalid) return false;

                var query = new byte[8];

                var outBuf = new byte[512];
                bool ok = DeviceIoControl(handle,
                    0x002D1400,
                    query, (uint)query.Length,
                    outBuf, (uint)outBuf.Length,
                    out _, IntPtr.Zero);

                if (!ok) return false;

                return CheckNoSeekPenalty(handle);
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(DetectSSD)); }
            return false;
        }

        private static bool CheckNoSeekPenalty(Microsoft.Win32.SafeHandles.SafeFileHandle handle)
        {
            try
            {
                var query = new byte[8];
                query[0] = 7;

                var outBuf = new byte[16];
                bool ok = DeviceIoControl(handle,
                    0x002D1400,
                    query, (uint)query.Length,
                    outBuf, (uint)outBuf.Length,
                    out _, IntPtr.Zero);

                if (!ok) return false;

                return outBuf.Length > 8 && outBuf[8] == 0;
            }
            catch { return false; }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
            string lpFileName, uint dwDesiredAccess, FileShare dwShareMode,
            IntPtr lpSecurityAttributes, FileMode dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            Microsoft.Win32.SafeHandles.SafeFileHandle hDevice, uint dwIoControlCode,
            byte[] lpInBuffer, uint nInBufferSize,
            byte[] lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);
    }
}