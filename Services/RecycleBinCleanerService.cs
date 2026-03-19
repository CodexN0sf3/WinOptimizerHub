using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WinOptimizerHub.Services
{
    public class RecycleBinCleanerService
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath,
            uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        public (long sizeBytes, long itemCount) GetRecycleBinInfo()
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
            try
            {
                SHQueryRecycleBin(null, ref info);
                return (info.i64Size, info.i64NumItems);
            }
            catch { return (0, 0); }
        }

        public async Task<bool> EmptyRecycleBinAsync(IProgress<string> progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report("Emptying Recycle Bin...");
                    uint result = SHEmptyRecycleBin(IntPtr.Zero, null,
                        SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                    return result == 0;
                }
                catch { return false; }
            });
        }
    }
}
