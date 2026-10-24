#if DEBUG
//#define SNAPSHOT_STACK_TRACE
#endif

using System.Windows.Input;
using AvaloniaLight.Reactive.Threading;

namespace AvaloniaLight.Reactive.Commands
{
    /// <summary>
    /// Base class for command implementations with CanExecute and resource disposal support.
    /// </summary>
    public abstract class ReactiveCommandBase<TParam, TResult> : ICommandDisposable
    {
        protected ReactiveCommandBase()
        {
#if SNAPSHOT_STACK_TRACE
            _stackTrace = new();
#endif
        }

        #region Fields

#if SNAPSHOT_STACK_TRACE
        /// <summary>
        /// Command creation stack (used in debug builds only).
        /// </summary>
        protected System.Diagnostics.StackTrace _stackTrace;
#endif

        protected bool _disposed;
        protected bool _isExecuting;

        private bool _canExecute;
        private EventHandler? _canExecuteChanged;

        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged
        {
            add => _canExecuteChanged += value;
            remove => _canExecuteChanged -= value;
        }

        #endregion

        #region ICommand

        /// <inheritdoc />
        void ICommand.Execute(object? parameter) => Execute(parameter);

        /// <summary>
        /// Executes the command with an object parameter.
        /// </summary>
        protected abstract void Execute(object? parameter);

        /// <inheritdoc />
        public bool CanExecute(object? parameter) => _canExecute && !_isExecuting;

        #endregion

        #region CanExecute helpers

        /// <summary>
        /// Sets whether the command can execute directly (without an Rx observable).
        /// </summary>
        public void SetCanExecute(bool canExecute) => OnCanExecuteChanged(canExecute);

        /// <summary>
        /// Updates the command's can-execute value.
        /// </summary>
        protected void OnCanExecuteChanged(bool newValue)
        {
            if (_canExecute == newValue)
                return;

            _canExecute = newValue;
            RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Notifies subscribers that CanExecute changed. The event is raised on the UI thread:
        /// immediately when called from the UI thread, via the dispatcher queue from other threads. A synchronous Invoke
        /// would block a background thread until the UI thread is free and could deadlock
        /// if the UI thread is meanwhile waiting on the background one.
        /// </summary>
        protected void RaiseCanExecuteChanged()
        {
            if (_canExecuteChanged is null)
                return;

            if (DispatcherHelper.CheckAccess())
                _canExecuteChanged?.Invoke(this, EventArgs.Empty);
            else
                DispatcherHelper.BeginInvoke(() => _canExecuteChanged?.Invoke(this, EventArgs.Empty));
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            //GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the command's resources.
        /// </summary>
        public abstract void Dispose(bool isDisposing);

        #endregion
    }
}