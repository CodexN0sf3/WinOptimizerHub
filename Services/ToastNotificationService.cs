using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public sealed class ToastNotificationService : IDisposable
    {
        public ObservableCollection<ToastItem> Toasts { get; } = new ObservableCollection<ToastItem>();

        private bool _disposed;
        private readonly Dispatcher _dispatcher;

        public ToastNotificationService()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        private static Brush AccentBrush(string hex) =>
            (Brush)new BrushConverter().ConvertFromString(hex);

        private static Brush CardBg(string accentHex)
        {

            var c = (Color)ColorConverter.ConvertFromString(accentHex);
            return new SolidColorBrush(Color.FromArgb(255,
                (byte)Math.Max(0, c.R / 12 + 8),
                (byte)Math.Max(0, c.G / 12 + 8),
                (byte)Math.Max(0, c.B / 12 + 8)));
        }


        public void ShowInfo(string title, string message = null, int timeoutMs = 5000) =>
            Enqueue(new ToastItem
            {
                Title = title,
                Message = message ?? string.Empty,
                Icon = "\uE946",
                AccentColor = AccentBrush("#3B82F6"),
                CardBackground = GetThemedCardBrush("#1A2535"),
            }, timeoutMs);

        public void ShowSuccess(string title, string message = null, int timeoutMs = 5000) =>
            Enqueue(new ToastItem
            {
                Title = title,
                Message = message ?? string.Empty,
                Icon = "\uE73E",
                AccentColor = AccentBrush("#22C55E"),
                CardBackground = GetThemedCardBrush("#0F2318"),
            }, timeoutMs);

        public void ShowWarning(string title, string message = null, int timeoutMs = 6000) =>
            Enqueue(new ToastItem
            {
                Title = title,
                Message = message ?? string.Empty,
                Icon = "\uE7BA",
                AccentColor = AccentBrush("#F59E0B"),
                CardBackground = GetThemedCardBrush("#1F1A0A"),
            }, timeoutMs);

        public void ShowError(string title, string message = null, int timeoutMs = 7000) =>
            Enqueue(new ToastItem
            {
                Title = title,
                Message = message ?? string.Empty,
                Icon = "\uEB90",
                AccentColor = AccentBrush("#EF4444"),
                CardBackground = GetThemedCardBrush("#210B0B"),
            }, timeoutMs);

        public void ShowCleanComplete(string operation, long bytesFreed, int errorsCount = 0)
        {
            string freed = FormatHelper.FormatSize(bytesFreed);
            string body = errorsCount > 0 ? $"Freed {freed}  ({errorsCount} errors)" : $"Freed {freed}";
            ShowSuccess($"✓ {operation} complete", body, 5000);
        }

        private void Enqueue(ToastItem item, int timeoutMs)
        {
            if (_disposed) return;
            _dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    Toasts.Add(item);
                    
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(timeoutMs) };
                    timer.Tick += (_, __) =>
                    {
                        timer.Stop();
                        Dismiss(item);
                    };
                    timer.Start();
                }
                catch (Exception ex) { AppLogger.Log(ex, nameof(ToastNotificationService)); }
            }));
        }

        public void Dismiss(ToastItem item)
        {
            if (_disposed) return;
            _dispatcher.BeginInvoke((Action)(() =>
            {
                try { Toasts.Remove(item); }
                catch { /* already removed */ }
            }));
        }

        private static Brush GetThemedCardBrush(string darkFallback)
        {
            try
            {
                if (Application.Current?.Resources["BgCardBrush"] is Brush b)
                    return b;
            }
            catch { }
            return AccentBrush(darkFallback);
        }

        public void Dispose() { _disposed = true; }
    }
}
