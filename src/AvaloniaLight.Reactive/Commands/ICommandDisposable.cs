using System.Windows.Input;

namespace AvaloniaLight.Reactive.Commands
{
    /// <summary>
    /// Command interface with support for resource disposal.
    /// </summary>
    public interface ICommandDisposable : ICommand, IDisposable
    {
        /// <summary>
        /// Sets whether the command can execute directly, without an Rx observable.
        /// </summary>
        /// <param name="canExecute">The new value of the can-execute state.</param>
        void SetCanExecute(bool canExecute);
    }
}