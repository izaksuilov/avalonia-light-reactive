using System.Windows.Input;

namespace LightReactive.Avalonia.Commands
{
    /// <summary>
    /// General-purpose methods for creating and using commands.
    /// </summary>
    public static class CommandHelper
    {
        #region Create

        /// <summary>
        /// Creates a synchronous command without parameters or a result.
        /// </summary>
        public static ICommand<Unit, Unit> Create(Action action, IObservable<bool>? canExecute = null)
        {
            return ReactiveCommand.Create(action, canExecute);
        }

        /// <summary>
        /// Creates a synchronous command with a parameter and no result.
        /// </summary>
        public static ICommand<TParam, Unit> Create<TParam>(Action<TParam> action, IObservable<bool>? canExecute = null)
        {
            return ReactiveCommand.Create(action, canExecute);
        }

        /// <summary>
        /// Creates a synchronous command without parameters, with a result.
        /// </summary>
        public static ICommand<Unit, TResult> Create<TResult>(Func<TResult> action, IObservable<bool>? canExecute = null)
        {
            return ReactiveCommand.Create(action, canExecute);
        }

        /// <summary>
        /// Creates a synchronous command with a parameter and a result.
        /// </summary>
        public static ICommand<TParam, TResult> Create<TParam, TResult>(Func<TParam, TResult> action, IObservable<bool>? canExecute = null)
        {
            return ReactiveCommand.Create(action, canExecute);
        }

        /// <summary>
        /// Creates a no-op command that performs no action.
        /// </summary>
        public static ICommand CreateStubCommand(bool canExecute = true)
        {
            return new StubCommandAsync<Unit, Unit>(canExecute);
        }

        #endregion

        #region Async Guards

        // Trap overloads. Without them, a method returning Task silently binds to
        // Create<TResult>(Func<TResult>) with TResult = Task: the command runs the method,
        // discards the returned Task and does not await it. An error inside such a command
        // goes unnoticed, while the calling code assumes the work has completed.
        // The compiler picks these overloads as the better match and reports an error right away.

        private const string ASYNC_GUARD_MESSAGE = "A method returning Task requires CommandHelper.CreateFromTask; otherwise the command will not await completion.";

        /// <summary>
        /// Prevents creating a synchronous command from an asynchronous method.
        /// </summary>
        [Obsolete(ASYNC_GUARD_MESSAGE, error: true)]
        public static ICommand<Unit, Task> Create(Func<Task> action, IObservable<bool>? canExecute = null)
            => throw new NotSupportedException(ASYNC_GUARD_MESSAGE);

        /// <summary>
        /// Prevents creating a synchronous command with a parameter from an asynchronous method.
        /// </summary>
        [Obsolete(ASYNC_GUARD_MESSAGE, error: true)]
        public static ICommand<TParam, Task> Create<TParam>(Func<TParam, Task> action, IObservable<bool>? canExecute = null)
            => throw new NotSupportedException(ASYNC_GUARD_MESSAGE);

        /// <summary>
        /// Prevents creating a synchronous command from an asynchronous method with a result.
        /// </summary>
        [Obsolete(ASYNC_GUARD_MESSAGE, error: true)]
        public static ICommand<Unit, Task<TResult>> Create<TResult>(Func<Task<TResult>> action, IObservable<bool>? canExecute = null)
            => throw new NotSupportedException(ASYNC_GUARD_MESSAGE);

        /// <summary>
        /// Prevents creating a synchronous command with a parameter from an asynchronous method with a result.
        /// </summary>
        [Obsolete(ASYNC_GUARD_MESSAGE, error: true)]
        public static ICommand<TParam, Task<TResult>> Create<TParam, TResult>(Func<TParam, Task<TResult>> action, IObservable<bool>? canExecute = null)
            => throw new NotSupportedException(ASYNC_GUARD_MESSAGE);

        #endregion

        #region Create From Task

        /// <summary>
        /// Creates an asynchronous command without parameters or a result.
        /// </summary>
        public static IAsyncCommand<Unit, Unit> CreateFromTask(Func<Task> action, IObservable<bool>? canExecute = null)
        {
            return ReactiveCommand.CreateFromTask(action, canExecute);
        }

        /// <summary>
        /// Creates an asynchronous command with a parameter and no result.
        /// </summary>
        public static IAsyncCommand<TParam, Unit> CreateFromTask<TParam>(Func<TParam, Task> action, IObservable<bool>? canExecute = null)
        {
            return ReactiveCommand.CreateFromTask(action, canExecute);
        }

        /// <summary>
        /// Creates an asynchronous command without parameters, with a result.
        /// </summary>
        public static IAsyncCommand<Unit, TResult> CreateFromTask<TResult>(Func<Task<TResult>> action, IObservable<bool>? canExecute = null)
        {
            return ReactiveCommand.CreateFromTask(action, canExecute);
        }

        /// <summary>
        /// Creates an asynchronous command with a parameter and a result.
        /// </summary>
        public static IAsyncCommand<TParam, TResult> CreateFromTask<TParam, TResult>(Func<TParam, Task<TResult>> action, IObservable<bool>? canExecute = null)
        {
            return ReactiveCommand.CreateFromTask(action, canExecute);
        }

        /// <summary>
        /// Creates an asynchronous no-op command that performs no action.
        /// </summary>
        public static IAsyncCommand<TParam, TResult> CreateStubCommand<TParam, TResult>(bool canExecute = true)
        {
            return new StubCommandAsync<TParam, TResult>(canExecute);
        }

        #endregion
    }

    /// <summary>
    /// No-op command for uniform use of the command interface.
    /// </summary>
    public class StubCommandAsync<TParam, TResult>(bool canExecute = true) : IAsyncCommand<TParam, TResult>
    {
        private readonly bool _canExecute = canExecute;

#pragma warning disable CS0067

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged;

#pragma warning restore CS0067

        /// <inheritdoc />
        public bool CanExecute(object? parameter) => _canExecute;

        /// <inheritdoc />
        public void SetCanExecute(bool canExecute)
        {
            // No-op: executability is fixed at creation.
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <summary>
        /// Executes the no-op command without parameters.
        /// </summary>
        public Task<TResult> Execute()
        {
            return Task.FromResult(default(TResult)!);
        }

        /// <summary>
        /// Executes the no-op command with a parameter.
        /// </summary>
        public Task<TResult> Execute(TParam parameter)
        {
            return Execute();
        }

        /// <summary>
        /// Executes the no-op command with an object parameter.
        /// </summary>
        public void Execute(object? parameter)
        {
        }
    }
}