namespace AvaloniaLight.Reactive.Commands
{
    /// <summary>
    /// Asynchronous command interface with a parameter and a result.
    /// </summary>
    public interface IAsyncCommand<TParam, TResult> : ICommandDisposable
    {
        /// <summary>
        /// Asynchronously executes the command without parameters.
        /// </summary>
        Task<TResult> Execute();

        /// <summary>
        /// Asynchronously executes the command with a parameter.
        /// </summary>
        Task<TResult> Execute(TParam parameter);
    }
}