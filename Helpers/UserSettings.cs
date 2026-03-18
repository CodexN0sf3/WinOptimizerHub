using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WinOptimizerHub.Helpers
{
    public class UserSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinOptimizerHub_Data", "settings.ini");

        private static readonly object _lock = new object();
        private static UserSettings _instance;

        public bool IsDarkTheme { get; set; } = false;
        public string LastPanel { get; set; } = "Dashboard";
        public double WindowWidth { get; set; } = 1280;
        public double WindowHeight { get; set; } = 780;
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public bool WindowMaximized { get; set; } = false;
        public string CleaningMode { get; set; } = "Safe";

        public static UserSettings Current
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }

        public static UserSettings Load()
        {
            var s = new UserSettings();
            try
            {
                if (!File.Exists(SettingsPath)) return s;

                foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    switch (key)
                    {
                        case "IsDarkTheme": s.IsDarkTheme = val == "true"; break;
                        //case "LastPanel": s.LastPanel = val; break;
                        case "WindowMaximized": s.WindowMaximized = val == "true"; break;
                        case "CleaningMode": s.CleaningMode = val; break;
                        case "WindowWidth":
                            if (double.TryParse(val, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double w) && w > 0)
                                s.WindowWidth = w;
                            break;
                        case "WindowHeight":
                            if (double.TryParse(val, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double h) && h > 0)
                                s.WindowHeight = h;
                            break;
                        case "WindowLeft":
                            if (double.TryParse(val, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double l))
                                s.WindowLeft = l;
                            break;
                        case "WindowTop":
                            if (double.TryParse(val, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double t))
                                s.WindowTop = t;
                            break;
                    }
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(UserSettings)); }
            return s;
        }

        public void Save()
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

                    var sb = new StringBuilder();
                    sb.AppendLine($"IsDarkTheme={IsDarkTheme.ToString().ToLower()}");
                    //sb.AppendLine($"LastPanel={LastPanel}");
                    sb.AppendLine($"WindowMaximized={WindowMaximized.ToString().ToLower()}");
                    sb.AppendLine($"WindowWidth={WindowWidth.ToString(CultureInfo.InvariantCulture)}");
                    sb.AppendLine($"WindowHeight={WindowHeight.ToString(CultureInfo.InvariantCulture)}");
                    sb.AppendLine($"WindowLeft={WindowLeft.ToString(CultureInfo.InvariantCulture)}");
                    sb.AppendLine($"WindowTop={WindowTop.ToString(CultureInfo.InvariantCulture)}");
                    sb.AppendLine($"CleaningMode={CleaningMode}");

                    File.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex) { AppLogger.Log(ex, nameof(Save)); }
        }
    }
}