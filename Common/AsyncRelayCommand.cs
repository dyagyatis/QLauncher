using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MinecraftLauncher.Common
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Predicate<object?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
            ArgumentNullException.ThrowIfNull(execute);
        }

        public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    RaiseCanExecuteChanged();
                }
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            try
            {
                IsExecuting = true;
                await _execute(parameter);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public async Task ExecuteAsync(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            try
            {
                IsExecuting = true;
                await _execute(parameter);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T?, Task> _execute;
        private readonly Predicate<T?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    RaiseCanExecuteChanged();
                }
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting) return false;

            if (parameter == null && typeof(T).IsValueType)
                return _canExecute?.Invoke(default) ?? true;

            return _canExecute?.Invoke((T?)parameter) ?? true;
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            try
            {
                IsExecuting = true;
                if (parameter == null && typeof(T).IsValueType)
                {
                    await _execute(default);
                    return;
                }
                await _execute((T?)parameter);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public async Task ExecuteAsync(T? parameter)
        {
            if (!CanExecute(parameter)) return;

            try
            {
                IsExecuting = true;
                await _execute(parameter);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
