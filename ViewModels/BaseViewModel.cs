using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.Services;

namespace WinOptimizerHub.ViewModels
{
    public abstract class BaseViewModel : ObservableObject
    {

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            protected set => SetProperty(ref _isBusy, value);
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            protected set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<string> ActivityLog { get; }

        private CancellationTokenSource _cts = new CancellationTokenSource();

        protected CancellationToken ResetCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        protected CancellationToken CurrentToken => _cts.Token;

        protected void CancelCurrent() => _cts?.Cancel();

        protected BaseViewModel(ObservableCollection<string> sharedLog)
        {
            ActivityLog = sharedLog ?? throw new ArgumentNullException(nameof(sharedLog));
        }

        protected void SetBusy(bool busy, string status = null)
        {
            IsBusy = busy;
            if (status != null)
                StatusMessage = status;
        }

        protected void ReportStatus(string message)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
                Application.Current.Dispatcher.Invoke(() => StatusMessage = message);
            else
                StatusMessage = message;
        }

        protected void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";

            void Append()
            {
                ActivityLog.Insert(0, entry);
                if (ActivityLog.Count > 200)
                    ActivityLog.RemoveAt(ActivityLog.Count - 1);
            }

            if (Application.Current?.Dispatcher?.CheckAccess() == false)
                Application.Current.Dispatcher.Invoke(Append);
            else
                Append();
        }

        protected IProgress<string> MakeProgress() =>
            new Progress<string>(msg => ReportStatus(msg));

        protected void InvalidateCommands()
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
                Application.Current.Dispatcher.Invoke(
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested);
            else
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        protected void HandleError(Exception ex, string context,
            string userMessage = null, ToastNotificationService toast = null)
        {
            AppLogger.Log(ex, context);
            SetBusy(false, userMessage ?? $"Error: {ex.Message}");
            toast?.ShowError(userMessage ?? "An error occurred", ex.Message);
        }

        public virtual void OnClose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}