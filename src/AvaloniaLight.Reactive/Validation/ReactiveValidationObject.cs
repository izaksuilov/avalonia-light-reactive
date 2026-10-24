using System.Collections;
using System.ComponentModel;

namespace AvaloniaLight.Reactive.Validation
{
    /// <summary>
    /// Base class for models with validation. Replacement for ReactiveUI.Validation.ReactiveValidationObject.
    /// Implements <see cref="INotifyDataErrorInfo"/>, so Avalonia error indicators (HasErrors/GetErrors) work.
    /// The validation context is created on first access: a model without rules has none at all.
    /// <see cref="ErrorsChanged"/> and the <see cref="HasErrors"/> notification are raised only on actual changes.
    /// </summary>
    public abstract class ReactiveValidationObject : ReactiveObject, IValidatableViewModel, INotifyDataErrorInfo, IDisposable, IValidationContextOwner
    {
        // Created on first access to ValidationContext; not created after Dispose.
        private ValidationContext? _validationContext;

        /// <summary>
        /// Validation context. Created on first access, thread-safe.
        /// If the model was disposed before the context was needed, the shared disposed context is returned.
        /// </summary>
        public ValidationContext ValidationContext => Volatile.Read(ref _validationContext) ?? CreateValidationContext();

        /// <summary>
        /// Indicates whether there are validation errors.
        /// </summary>
        public bool HasErrors => Volatile.Read(ref _validationContext) is { } context && !context.GetIsValid();

        /// <inheritdoc />
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        /// <inheritdoc />
        public IEnumerable GetErrors(string? propertyName)
            => Volatile.Read(ref _validationContext)?.GetMessages(propertyName) ?? Array.Empty<string>();

        /// <summary>
        /// The context if it has already been created. Reading does not create a context.
        /// </summary>
        internal ValidationContext? ExistingValidationContext => Volatile.Read(ref _validationContext);

        /// <inheritdoc />
        public virtual void Dispose()
        {
            // There is no context yet: install the shared disposed one so that access after Dispose does not create a new one.
            // A created context is disposed and kept on the model: HasErrors after Dispose behaves as before.
            ValidationContext? context = Interlocked.CompareExchange(ref _validationContext, ValidationContext.DisposedInstance, null);
            context?.Dispose();
        }

        private ValidationContext CreateValidationContext()
        {
            // The owner is set before publication: a rule that another thread adds right after it
            // will already report the error to the model.
            ValidationContext created = new(this);
            ValidationContext? existing = Interlocked.CompareExchange(ref _validationContext, created, null);
            if (existing is null)
                return created;

            // Another thread has already created the context, or the model is disposed: ours is not needed.
            created.Dispose();
            return existing;
        }

        void IValidationContextOwner.OnErrorsChanged(string? propertyName)
            => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));

        void IValidationContextOwner.OnValidityChanged()
            => RaisePropertyChanged(nameof(HasErrors));
    }
}
