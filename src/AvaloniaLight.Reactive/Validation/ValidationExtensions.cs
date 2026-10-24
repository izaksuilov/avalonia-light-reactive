using System.Linq.Expressions;

namespace AvaloniaLight.Reactive.Validation
{
    /// <summary>
    /// Methods for creating and removing validation rules. Replacement for ReactiveUI.Validation.Extensions (the overloads in use).
    /// Called as this.ValidationRule(...); signatures mirror ReactiveUI.
    /// </summary>
    public static class ValidationExtensions
    {
        /// <summary>
        /// Property rule: a validity predicate and a fixed error message.
        /// </summary>
        public static ValidationHelper ValidationRule<TViewModel, TProperty>(
            this TViewModel viewModel,
            Expression<Func<TViewModel, TProperty>> property,
            Func<TProperty, bool> isPropertyValid,
            string message)
            where TViewModel : class, IValidatableViewModel
            => viewModel.ValidationRule(property, isPropertyValid, _ => message);

        /// <summary>
        /// Property rule: a validity predicate and a message factory function.
        /// </summary>
        public static ValidationHelper ValidationRule<TViewModel, TProperty>(
            this TViewModel viewModel,
            Expression<Func<TViewModel, TProperty>> property,
            Func<TProperty, bool> isPropertyValid,
            Func<TProperty, string> messageFunc)
            where TViewModel : class, IValidatableViewModel
        {
            IObservable<TProperty> source = viewModel.WhenAnyValue(property);
            return AddRule(viewModel, GetLeafName(property), source, isPropertyValid, messageFunc);
        }

        /// <summary>
        /// Rule over an arbitrary value stream: a validity predicate and a message function.
        /// </summary>
        public static ValidationHelper ValidationRule<TViewModel, TValue>(
            this TViewModel viewModel,
            IObservable<TValue> validationObservable,
            Func<TValue, bool> isValid,
            Func<TValue, string> messageFunc)
            where TViewModel : class, IValidatableViewModel
            => AddRule(viewModel, null, validationObservable, isValid, messageFunc);

        /// <summary>
        /// Rule bound to a property but validated against a separate state stream (cross-field validation).
        /// </summary>
        public static ValidationHelper ValidationRule<TViewModel, TProperty, TState>(
            this TViewModel viewModel,
            Expression<Func<TViewModel, TProperty>> property,
            IObservable<TState> validationObservable,
            Func<TState, bool> isValid,
            Func<TState, string> messageFunc)
            where TViewModel : class, IValidatableViewModel
            => AddRule(viewModel, GetLeafName(property), validationObservable, isValid, messageFunc);

        /// <summary>
        /// Removes all of the model's rules and disposes their subscriptions. Replacement for ClearValidationRules from ReactiveUI.Validation.
        /// The context remains usable: subscriptions to <see cref="ValidationContext.Valid"/> are kept, and new rules are added as usual.
        /// </summary>
        public static void ClearValidationRules<TViewModel>(this TViewModel viewModel)
            where TViewModel : class, IValidatableViewModel
            => GetExistingContext(viewModel)?.RemoveAll(static _ => true);

        /// <summary>
        /// Removes rules bound to the property and disposes their subscriptions.
        /// The property is matched by the name of the last segment of the path, as bindings see it:
        /// x =&gt; x.A.B removes the rules of property B. Rules over an arbitrary stream without a property are not removed.
        /// </summary>
        public static void ClearValidationRules<TViewModel, TProperty>(
            this TViewModel viewModel,
            Expression<Func<TViewModel, TProperty>> property)
            where TViewModel : class, IValidatableViewModel
        {
            string propertyName = GetLeafName(property)
                ?? throw new ArgumentException("A property access expression is expected, for example x => x.Name.", nameof(property));
            GetExistingContext(viewModel)?.RemoveAll(component => component.PropertyName == propertyName);
        }

        private static ValidationHelper AddRule<TViewModel, T>(
            TViewModel viewModel,
            string? propertyName,
            IObservable<T> source,
            Func<T, bool> isValid,
            Func<T, string> messageFunc)
            where TViewModel : class, IValidatableViewModel
        {
            ValidationContext context = viewModel.ValidationContext;
            ValidationComponent<T> component = new(propertyName, source, isValid, messageFunc);
            context.Add(component);
            return new ValidationHelper(context, component);
        }

        // There are no rules to remove from a model that has no context yet: no context is created for that.
        private static ValidationContext? GetExistingContext<TViewModel>(TViewModel viewModel)
            where TViewModel : class, IValidatableViewModel
            => viewModel is ReactiveValidationObject model ? model.ExistingValidationContext : viewModel.ValidationContext;

        private static string? GetLeafName(LambdaExpression property)
        {
            Expression? expr = WhenAnyMixin.Unwrap(property.Body);
            return expr is MemberExpression member ? member.Member.Name : null;
        }
    }
}
