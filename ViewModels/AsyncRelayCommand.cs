using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinOptimizerHub.Helpers;

namespace WinOptimizerHub.ViewModels
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private readonly Action<Exception> _onError;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null,
                                 Action<Exception> onError = null)
            : this(_ => execute(),
                   canExecute == null ? (Func<object, bool>)null : _ => canExecute(),
                   onError)
        { }

        public AsyncRelayCommand(Func<object, Task> execute,
                                 Func<object, bool> canExecute = null,
                                 Action<Exception> onError = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) =>
            !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

        public async void Execute(object parameter) => await ExecuteAsync(parameter);

        public async Task ExecuteAsync(object parameter = null)
        {
            if (_isExecuting) return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute(parameter);
            }
            catch (OperationCanceledException)
            {
                /*Expected — user cancelled, no log needed*/
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "AsyncRelayCommand");

                if (_onError != null)
                {
                    if (Application.Current?.Dispatcher?.CheckAccess() == false)
                        Application.Current.Dispatcher.Invoke(() => _onError(ex));
                    else
                        _onError(ex);
                }
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
                Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
            else
                CommandManager.InvalidateRequerySuggested();
        }
    }
}