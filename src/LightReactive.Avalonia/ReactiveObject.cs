using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LightReactive.Avalonia
{
    /// <summary>
    /// Lightweight model base with property change notifications.
    /// Replacement for ReactiveUI.ReactiveObject without the heavy infrastructure (ConditionalWeakTable, schedulers, etc.).
    /// Properties are set via <see cref="RaiseAndSetIfChanged"/> - it is also used by the source generator's
    /// <c>[Reactive]</c> attribute.
    /// </summary>
    public abstract class ReactiveObject : INotifyPropertyChanged, INotifyPropertyChanging
    {
        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc />
        public event PropertyChangingEventHandler? PropertyChanging;

        /// <summary>
        /// Raises a notification that a property has changed.
        /// </summary>
        public void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Raises a notification that a property is about to change.
        /// </summary>
        public void RaisePropertyChanging([CallerMemberName] string? propertyName = null)
            => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

        /// <summary>
        /// If the value has changed, stores it in the field and raises notifications.
        /// Base method for properties; it is also called by the implementation of the <c>[Reactive]</c> attribute.
        /// </summary>
        /// <returns><see langword="true"/> if the value has changed.</returns>
        protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            RaisePropertyChanging(propertyName);
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }
    }
}
