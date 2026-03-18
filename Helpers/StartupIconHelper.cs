using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinOptimizerHub.Helpers
{
    public static class StartupIconHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex,
            IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly ConcurrentDictionary<string, ImageSource> _cache
            = new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<int, ImageSource> _shell32Cache
            = new ConcurrentDictionary<int, ImageSource>();

        private static readonly string Shell32Path =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");

        private static readonly int Shell32_Generic = 0;
        private static readonly int Shell32_Exe = 2;
        private static readonly int Shell32_Dll = 72;
        private static readonly int Shell32_Cmd = 154;
        private static readonly int Shell32_Script = 1;

        public static ImageSource GetIcon(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return GetShell32Icon(Shell32_Generic);

            string path = Environment.ExpandEnvironmentVariables(exePath.Trim().Trim('"'));
            path = path.Trim();

            if (_cache.TryGetValue(path, out var cached))
                return cached;

            ImageSource result = ExtractFromFile(path);
            _cache[path] = result;
            return result;
        }

        private static ImageSource ExtractFromFile(string path)
        {
            if (!File.Exists(path))
                return GetShell32Icon(Shell32_Generic);

            try
            {
                IntPtr[] large = new IntPtr[1];
                IntPtr[] small = new IntPtr[1];
                uint count = ExtractIconEx(path, 0, large, small, 1);

                IntPtr hIcon = large[0] != IntPtr.Zero ? large[0]
                             : small[0] != IntPtr.Zero ? small[0]
                             : IntPtr.Zero;

                if (hIcon != IntPtr.Zero && hIcon != (IntPtr)1)
                {
                    try
                    {
                        var src = HIconToImageSource(hIcon);
                        if (src != null) return src;
                    }
                    finally
                    {
                        if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                        if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
                    }
                }
            }
            catch { }

            return GetFallbackByExtension(path);
        }

        private static ImageSource GetFallbackByExtension(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".exe" => GetShell32Icon(Shell32_Exe),
                ".dll" => GetShell32Icon(Shell32_Dll),
                ".cmd" or ".bat" => GetShell32Icon(Shell32_Cmd),
                ".ps1" or ".vbs" or ".js" => GetShell32Icon(Shell32_Script),
                _ => GetShell32Icon(Shell32_Generic)
            };
        }

        public static ImageSource GetShell32Icon(int index)
        {
            if (_shell32Cache.TryGetValue(index, out var cached))
                return cached;

            try
            {
                IntPtr[] large = new IntPtr[1];
                IntPtr[] small = new IntPtr[1];
                ExtractIconEx(Shell32Path, index, large, small, 1);

                IntPtr hIcon = large[0] != IntPtr.Zero ? large[0] : small[0];
                if (hIcon != IntPtr.Zero && hIcon != (IntPtr)1)
                {
                    try
                    {
                        var src = HIconToImageSource(hIcon);
                        if (src != null)
                        {
                            _shell32Cache[index] = src;
                            return src;
                        }
                    }
                    finally
                    {
                        if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                        if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
                    }
                }
            }
            catch { }

            return null;
        }

        private static ImageSource HIconToImageSource(IntPtr hIcon)
        {
            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(20, 20));
                src.Freeze();
                return src;
            }
            catch { return null; }
        }
    }
}