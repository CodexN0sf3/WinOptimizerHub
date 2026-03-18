using WinOptimizerHub.Models;

public class BrowserInfo : ObservableObject
{
    public long CacheSize { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public long Size { get; set; }
    public string CachePath { get; set; } = string.Empty;
    public string HistoryPath { get; set; } = string.Empty;
    public string CookiesPath { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public bool IsSelected { get; set; } = true;
    public string CachePathDisplay => !string.IsNullOrEmpty(Path) ? Path : CachePath;
    public string CacheSizeDisplay => WinOptimizerHub.Converters.FileSizeConverter.FormatSize(Size > 0 ? Size : CacheSize);

    private string _details = string.Empty;
    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public enum BrowserCategory { Browser, WebView, ChromiumStandalone }
    public BrowserCategory Category { get; set; } = BrowserCategory.Browser;
}