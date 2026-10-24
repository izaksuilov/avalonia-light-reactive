using System.ComponentModel;

namespace AvaloniaLight.Reactive.Validation
{
    /// <summary>
    /// A model that supports validation. Replacement for ReactiveUI.Validation.IValidatableViewModel.
    /// </summary>
    public interface IValidatableViewModel
    {
        /// <summary>
        /// The model's validation context.
        /// </summary>
        ValidationContext ValidationContext { get; }
    }

    /// <summary>
    /// A single validation rule within a context.
    /// </summary>
    internal interface IValidationComponent
    {
        /// <summary>
        /// Name of the property the rule is bound to (for INotifyDataErrorInfo). May be null.
        /// </summary>
        string? PropertyName { get; }

        /// <summary>
        /// Current validation result.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Current error message (null if the rule is satisfied).
        /// </summary>
        string? Message { get; }

        /// <summary>
        /// The context to which the rule reports result changes.
        /// The context sets it when it accepts the rule and clears it when the rule is removed.
        /// Changes only under the context lock: it lets the context know exactly whether the rule belongs to it.
        /// </summary>
        ValidationContext? Context { get; set; }
    }

    /// <summary>
    /// Context owner: receives notifications about actual error changes.
    /// Set in the context constructor, i.e. before other threads can see the context.
    /// </summary>
    internal interface IValidationContextOwner
    {
        /// <summary>
        /// A property's error list changed (null: rules without a property).
        /// </summary>
        void OnErrorsChanged(string? propertyName);

        /// <summary>
        /// The overall validation result changed.
        /// </summary>
        void OnValidityChanged();
    }

    /// <summary>
    /// Validation context: aggregates rules and reports the overall result.
    /// Replacement for ReactiveUI.Validation.ValidationContext. It is a ReactiveObject so that IsValid notifications can be raised.
    /// Notifies only about actual changes: a valid rule being added or removed does not change errors and raises no events.
    /// </summary>
    public sealed class ValidationContext : ReactiveObject, INotifyPropertyChanged, IDisposable
    {
        // The rule list also serves as the lock object: a separate object per context is not needed.
        private readonly List<IValidationComponent> _components = new();
        private IValidationContextOwner? _owner;
        // The result stream is created on first access to Valid: not every model has subscribers.
        private BehaviorSubject<bool>? _validSubject;
        private bool _isValid = true;
        private bool _disposed;

        /// <summary>
        /// Constructor for a standalone context, without an owner model.
        /// </summary>
        public ValidationContext()
        {
        }

        /// <summary>
        /// Constructor for a model's context. The owner receives error notifications.
        /// </summary>
        internal ValidationContext(IValidationContextOwner owner) => _owner = owner;

        /// <summary>
        /// Shared disposed context. It is returned for a model that was disposed
        /// before it needed its own context: this way no new context is created after Dispose.
        /// </summary>
        internal static ValidationContext DisposedInstance { get; } = CreateDisposed();

        /// <summary>
        /// Validation result stream (the current value is emitted immediately on subscription).
        /// A disposed context also has a disposed stream: subscribing throws <see cref="ObjectDisposedException"/>.
        /// </summary>
        public IObservable<bool> Valid => Volatile.Read(ref _validSubject) ?? CreateValidSubject();

        /// <summary>
        /// Current overall validation result.
        /// </summary>
        public bool IsValid => _isValid;

        /// <summary>
        /// Returns the current overall validation result.
        /// </summary>
        public bool GetIsValid() => _isValid;

        #region Rules

        // The rule set and the overall result change under a single lock, while notifications are raised outside it.

        internal void Add(IValidationComponent component)
        {
            bool accepted;
            bool invalid = false;
            ValidityChange? validity = null;
            lock (_components)
            {
                accepted = !_disposed;
                if (accepted)
                {
                    _components.Add(component);
                    component.Context = this;
                    invalid = !component.IsValid;
                    validity = UpdateValidity();
                }
            }

            if (!accepted)
            {
                // A disposed context does not accept rules. The rule is unsubscribed from its source,
                // otherwise it would keep computing for nothing.
                (component as IDisposable)?.Dispose();
                return;
            }

            // A valid rule changes neither the overall result nor the error list: nothing to notify about.
            Notify(component.PropertyName, invalid, validity);
        }

        internal void Remove(IValidationComponent component)
        {
            bool invalid;
            ValidityChange? validity;
            lock (_components)
            {
                // The rule may have already been removed via ClearValidationRules: nothing to notify about then.
                if (_disposed || !_components.Remove(component))
                    return;

                component.Context = null;
                invalid = !component.IsValid;
                validity = UpdateValidity();
            }

            // A removed valid rule produced no errors: the error list did not change.
            Notify(component.PropertyName, invalid, validity);
        }

        /// <summary>
        /// Removes rules matching the predicate and disposes their subscriptions.
        /// </summary>
        internal void RemoveAll(Func<IValidationComponent, bool> match)
        {
            List<IValidationComponent>? removed = null;
            ValidityChange? validity;
            lock (_components)
            {
                if (_disposed)
                    return;

                _components.RemoveAll(component =>
                {
                    if (!match(component))
                        return false;

                    (removed ??= new()).Add(component);
                    return true;
                });

                if (removed is null)
                    return;

                foreach (IValidationComponent component in removed)
                    component.Context = null;

                validity = UpdateValidity();
            }

            foreach (IValidationComponent component in removed)
                (component as IDisposable)?.Dispose();

            RaiseValidity(validity);

            // Bindings request errors by property name, so one notification per property is enough.
            // Valid rules produced no errors, so their removal is not notified.
            foreach (IValidationComponent component in removed.Where(static c => !c.IsValid).DistinctBy(static c => c.PropertyName))
                NotifyOwner(component.PropertyName, errorsChanged: true, validityChanged: false);

            NotifyOwner(null, errorsChanged: false, validity is not null);
        }

        internal IEnumerable<string> GetMessages(string? propertyName)
        {
            List<string>? messages = null;
            lock (_components)
            {
                foreach (IValidationComponent component in _components)
                {
                    if (!component.IsValid
                        && component.Message is { } message
                        && (propertyName is null || component.PropertyName == propertyName))
                    {
                        (messages ??= new()).Add(message);
                    }
                }
            }

            return messages ?? (IEnumerable<string>)Array.Empty<string>();
        }

        /// <summary>
        /// A rule reports that its result or message changed.
        /// </summary>
        internal void OnComponentChanged(IValidationComponent component)
        {
            ValidityChange? validity;
            lock (_components)
            {
                // The rule may have read Context before it was removed on another thread.
                // Context changes only under this lock, so the check is exact:
                // a removed rule and a rule of a disposed context raise no events.
                if (component.Context != this)
                    return;

                validity = UpdateValidity();
            }

            Notify(component.PropertyName, errorsChanged: true, validity);
        }

        #endregion

        #region Result and notifications

        /// <summary>
        /// An overall result change detected under the lock. Notifications about it are raised outside the lock.
        /// </summary>
        private readonly record struct ValidityChange(bool IsValid, BehaviorSubject<bool>? Subject);

        /// <summary>
        /// Recalculates the overall result. Call under the lock.
        /// Returns null if the result did not change.
        /// </summary>
        private ValidityChange? UpdateValidity()
        {
            bool isValid = AreAllValid();
            if (isValid == _isValid)
                return null;

            // The result and the stream are read under the same lock that guards stream creation in Valid:
            // a new stream will not receive a stale result.
            _isValid = isValid;
            return new ValidityChange(isValid, _validSubject);
        }

        // Call under the lock.
        private bool AreAllValid()
        {
            foreach (IValidationComponent component in _components)
            {
                if (!component.IsValid)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Raises notifications: first about the context result, then to the owner.
        /// Call outside the lock: subscriber handlers must not run under it.
        /// </summary>
        private void Notify(string? propertyName, bool errorsChanged, ValidityChange? validity)
        {
            RaiseValidity(validity);
            NotifyOwner(propertyName, errorsChanged, validity is not null);
        }

        private void RaiseValidity(ValidityChange? validity)
        {
            if (validity is not { } change)
                return;

            RaisePropertyChanging(nameof(IsValid));
            RaisePropertyChanged(nameof(IsValid));
            change.Subject?.OnNext(change.IsValid);
        }

        private void NotifyOwner(string? propertyName, bool errorsChanged, bool validityChanged)
        {
            IValidationContextOwner? owner = Volatile.Read(ref _owner);
            if (owner is null)
                return;

            if (errorsChanged)
                owner.OnErrorsChanged(propertyName);

            if (validityChanged)
                owner.OnValidityChanged();
        }

        private BehaviorSubject<bool> CreateValidSubject()
        {
            lock (_components)
            {
                if (_validSubject is null)
                {
                    BehaviorSubject<bool> subject = new(_isValid);
                    // A disposed context returns a disposed stream, as before.
                    if (_disposed)
                        subject.Dispose();

                    Volatile.Write(ref _validSubject, subject);
                }

                return _validSubject;
            }
        }

        // A disposed context no longer changes or notifies. It accepts no new subscribers:
        // otherwise the shared disposed context would accumulate them forever. Unsubscribing always works.
        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                if (!Volatile.Read(ref _disposed))
                    PropertyChanged += value;
            }
            remove => PropertyChanged -= value;
        }

        #endregion

        #region Disposal

        /// <inheritdoc />
        public void Dispose()
        {
            IValidationComponent[] components;
            BehaviorSubject<bool>? subject;
            lock (_components)
            {
                if (_disposed)
                    return;

                _disposed = true;
                Volatile.Write(ref _owner, null);
                components = _components.ToArray();
                _components.Clear();
                _components.TrimExcess();
                foreach (IValidationComponent component in components)
                    component.Context = null;

                subject = _validSubject;
            }

            foreach (IValidationComponent component in components)
                (component as IDisposable)?.Dispose();

            subject?.Dispose();
        }

        private static ValidationContext CreateDisposed()
        {
            ValidationContext context = new();
            context.Dispose();
            return context;
        }

        #endregion
    }

    /// <summary>
    /// Validation rule handle. Replacement for ReactiveUI.Validation.ValidationHelper.
    /// Disposing removes the rule from the context.
    /// </summary>
    public sealed class ValidationHelper : IDisposable
    {
        private readonly ValidationContext _context;
        private readonly IValidationComponent _component;

        internal ValidationHelper(ValidationContext context, IValidationComponent component)
        {
            _context = context;
            _component = component;
        }

        /// <summary>
        /// Current rule check result.
        /// </summary>
        public bool IsValid => _component.IsValid;

        /// <summary>
        /// Current error message (null if the rule is satisfied).
        /// </summary>
        public string? Message => _component.Message;

        /// <inheritdoc />
        public void Dispose()
        {
            _context.Remove(_component);
            (_component as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Validation rule based on an observable value.
    /// </summary>
    internal sealed class ValidationComponent<T> : IValidationComponent, IDisposable
    {
        private readonly Func<T, bool> _isValid;
        private readonly Func<T, string> _messageFunc;
        private IDisposable? _subscription;
        private ValidationContext? _context;

        public ValidationComponent(string? propertyName, IObservable<T> source, Func<T, bool> isValid, Func<T, string> messageFunc)
        {
            PropertyName = propertyName;
            _isValid = isValid;
            _messageFunc = messageFunc;
            // Subscription via IObserver: unambiguous and independent of the Subscribe operators.
            // The first value arrives here, while there is no context yet: the context reads its result itself when the rule is added.
            _subscription = source.Subscribe(new AnonymousObserver<T>(OnValue));
        }

        public string? PropertyName { get; }

        public bool IsValid { get; private set; } = true;

        public string? Message { get; private set; }

        public ValidationContext? Context
        {
            get => Volatile.Read(ref _context);
            set => Volatile.Write(ref _context, value);
        }

        private void OnValue(T value)
        {
            bool valid;
            try { valid = _isValid(value); }
            catch { valid = false; }

            string? message = null;
            if (!valid)
            {
                try { message = _messageFunc(value); }
                catch { message = null; }
            }

            if (valid == IsValid && message == Message)
                return;

            IsValid = valid;
            Message = message;
            Context?.OnComponentChanged(this);
        }

        // The rule is disposed by both ClearValidationRules and ValidationHelper.Dispose,
        // and a subscription to a foreign stream is not required to tolerate repeated disposal.
        public void Dispose() => Interlocked.Exchange(ref _subscription, null)?.Dispose();
    }
}
