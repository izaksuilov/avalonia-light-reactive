using AvaloniaLight.Reactive.Linq;

namespace AvaloniaLight.Reactive.Commands
{
    /// <summary>
    /// Synchronous command with a parameter and a result.
    /// </summary>
    public class ReactiveCommand<TParam, TResult> : ReactiveCommandBase<TParam, TResult>, ICommand<TParam, TResult>
    {
        private Func<TParam, TResult>? _action;
        private CompositeDisposable? _disposables;

        #region Construction

        internal ReactiveCommand(Func<TParam, TResult> action, IObservable<bool>? canExecute)
        {
            _action = action;

            if (canExecute is null)
            {
                OnCanExecuteChanged(true);
            }
            else
            {
                _disposables = new()
                {
                    canExecute.Subscribe(OnCanExecuteChanged)
                };
            }
        }

        #endregion

        #region Execute API

        /// <summary>
        /// Executes the command without parameters.
        /// </summary>
        public TResult Execute()
        {
            return ExecuteInternal(default!);
        }

        /// <summary>
        /// Executes the command with a parameter.
        /// </summary>
        public TResult Execute(TParam parameter)
        {
            return ExecuteInternal(parameter);
        }

        /// <summary>
        /// Internal implementation of command execution, taking the current state into account.
        /// </summary>
        private TResult ExecuteInternal(TParam parameter)
        {
            if (_isExecuting)
                return default!;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                if (!CanInvokeAction())
                    return default!;

                return _action!.Invoke(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Checks whether the command action can be invoked.
        /// </summary>
        private bool CanInvokeAction()
        {
            if (_action is null || _disposed)
            {
#if DEBUG
                // attempt to invoke the command after _action was cleared
                System.Diagnostics.Debugger.Break();
#endif
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        protected override void Execute(object? parameter)
        {
            if (parameter is null)
            {
                Execute();
                return;
            }

            if (parameter is not TParam tParam)
                throw new InvalidOperationException($"Command requires parameters of type {typeof(TParam).FullName}, but received parameter of type {parameter.GetType().FullName}.");

            Execute(tParam);
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public override void Dispose(bool isDisposing)
        {
            if (_disposed)
                return;

            _disposables?.Dispose();
            _action = null;

            _disposed = true;
        }

        #endregion
    }
}