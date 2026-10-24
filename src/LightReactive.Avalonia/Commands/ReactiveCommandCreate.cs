namespace LightReactive.Avalonia.Commands
{
    /// <summary>
    /// Factory methods for creating commands.
    /// </summary>
    public static class ReactiveCommand
    {
        #region Create sync

        /// <summary>
        /// Creates a synchronous command without parameters or a result.
        /// </summary>
        internal static ReactiveCommand<Unit, Unit> Create(Action action, IObservable<bool>? canExecute)
        {
            return new ReactiveCommand<Unit, Unit>(
                u =>
                {
                    action.Invoke();
                    return default;
                },
                canExecute);
        }

        /// <summary>
        /// Creates a synchronous command with a parameter and no result.
        /// </summary>
        internal static ReactiveCommand<TParam, Unit> Create<TParam>(Action<TParam> action, IObservable<bool>? canExecute)
        {
            return new ReactiveCommand<TParam, Unit>(
                param =>
                {
                    action.Invoke(param);
                    return default;
                },
                canExecute);
        }

        /// <summary>
        /// Creates a synchronous command without parameters, with a result.
        /// </summary>
        internal static ReactiveCommand<Unit, TResult> Create<TResult>(Func<TResult> action, IObservable<bool>? canExecute)
        {
            return new ReactiveCommand<Unit, TResult>(
                u => action.Invoke(),
                canExecute);
        }

        /// <summary>
        /// Creates a synchronous command with a parameter and a result.
        /// </summary>
        internal static ReactiveCommand<TParam, TResult> Create<TParam, TResult>(Func<TParam, TResult> action, IObservable<bool>? canExecute)
        {
            return new ReactiveCommand<TParam, TResult>(
                action,
                canExecute);
        }

        #endregion

        #region Create async

        /// <summary>
        /// Creates an asynchronous command without parameters or a result.
        /// </summary>
        internal static ReactiveCommandAsync<Unit, Unit> CreateFromTask(Func<Task> action, IObservable<bool>? canExecute = null)
        {
            return new ReactiveCommandAsync<Unit, Unit>(
                async _ =>
                {
                    await action.Invoke();
                    return default;
                },
                canExecute);
        }

        /// <summary>
        /// Creates an asynchronous command with a parameter and no result.
        /// </summary>
        internal static ReactiveCommandAsync<TParam, Unit> CreateFromTask<TParam>(Func<TParam, Task> action, IObservable<bool>? canExecute = null)
        {
            return new ReactiveCommandAsync<TParam, Unit>(
                async param =>
                {
                    await action.Invoke(param);
                    return default;
                },
                canExecute);
        }

        /// <summary>
        /// Creates an asynchronous command without parameters, with a result.
        /// </summary>
        internal static ReactiveCommandAsync<Unit, TResult> CreateFromTask<TResult>(Func<Task<TResult>> action, IObservable<bool>? canExecute = null)
        {
            return new ReactiveCommandAsync<Unit, TResult>(
                async _ => await action.Invoke(),
                canExecute);
        }

        /// <summary>
        /// Creates an asynchronous command with a parameter and a result.
        /// </summary>
        internal static ReactiveCommandAsync<TParam, TResult> CreateFromTask<TParam, TResult>(Func<TParam, Task<TResult>> action, IObservable<bool>? canExecute = null)
        {
            return new ReactiveCommandAsync<TParam, TResult>(
                action,
                canExecute);
        }

        #endregion
    }
}