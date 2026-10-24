using AvaloniaLight.Reactive.Linq;

namespace AvaloniaLight.Reactive.Commands
{
    /// <summary>
    /// Asynchronous command with a parameter and a result.
    /// </summary>
    public class ReactiveCommandAsync<TParam, TResult> : ReactiveCommandBase<TParam, TResult>, IAsyncCommand<TParam, TResult>
    {
        private Func<TParam, Task<TResult>>? _action;
        private CompositeDisposable? _disposables;

        #region Construction

        internal ReactiveCommandAsync(Func<TParam, Task<TResult>> action, IObservable<bool>? canExecute)
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
        /// Asynchronously executes the command without parameters.
        /// </summary>
        public Task<TResult> Execute()
        {
            return ExecuteInternalAsync(default!);
        }

        /// <summary>
        /// Asynchronously executes the command with a parameter.
        /// </summary>
        public Task<TResult> Execute(TParam parameter)
        {
            return ExecuteInternalAsync(parameter);
        }

        /// <summary>
        /// Internal implementation of asynchronous command execution.
        /// </summary>
        private async Task<TResult> ExecuteInternalAsync(TParam parameter)
        {
            if (_isExecuting)
                return default!;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                if (!CanInvokeAction())
                    return default!;

                return await _action!.Invoke(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Checks whether the asynchronous command action can be invoked.
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
        protected override async void Execute(object? parameter)
        {
            if (parameter is null)
            {
                await Execute();
                return;
            }

            if (parameter is not TParam tParam)
                throw new InvalidOperationException($"Command requires parameters of type {typeof(TParam).FullName}, but received parameter of type {parameter.GetType().FullName}.");

            await Execute(tParam);
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