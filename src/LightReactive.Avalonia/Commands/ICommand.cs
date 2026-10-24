namespace LightReactive.Avalonia.Commands
{
    /// <summary>
    /// Synchronous command interface with a parameter and a result.
    /// </summary>
    public interface ICommand<TParam, TResult> : ICommandDisposable
    {
        /// <summary>
        /// Executes the command without parameters.
        /// </summary>
        TResult Execute();

        /// <summary>
        /// Executes the command with a parameter.
        /// </summary>
        TResult Execute(TParam param);
    }
}