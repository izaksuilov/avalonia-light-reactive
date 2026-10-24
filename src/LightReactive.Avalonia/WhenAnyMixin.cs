using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using LightReactive.Avalonia.Linq;

namespace LightReactive.Avalonia
{
    /// <summary>
    /// Observes changes of model properties. Replacement for ReactiveUI.WhenAnyValue.
    /// Supports nested paths (x =&gt; x.A.B) with resubscription at intermediate levels.
    /// On subscription it immediately emits the current value (like ReactiveUI).
    /// It does not re-emit the previous value (like ReactiveUI): a PropertyChanged notification without a value change
    /// is silent. For several properties, repeats are suppressed per property, and the selector result
    /// is not compared. The isDistinct: false parameter enables emission on every notification.
    /// </summary>
    public static class WhenAnyMixin
    {
        /// <summary>
        /// Observes the value of a (possibly nested) property.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, TRet>(this TSender sender,
            Expression<Func<TSender, TRet>> property,
            bool isDistinct = true)
        {
            PropertyInfo[] chain = GetPropertyChain(property.Body);
            Func<object, TRet>? convert = ValueConverter<TRet>.For(chain.Length == 0 ? typeof(TSender) : chain[^1].PropertyType);
            return new AnonymousObservable<TRet>(observer =>
            {
                PropertyChainObserver<TRet> watcher = new(sender!, chain, convert, isDistinct, observer);
                watcher.Start();
                return watcher;
            });
        }

        /// <summary>
        /// Observes a property and projects its value through a selector.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property,
            Func<T1, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property, isDistinct).Select(selector);

        /// <summary>
        /// Observes two properties and combines their values.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, T2, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Func<T1, T2, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, isDistinct).CombineLatest(sender.WhenAnyValue(property2, isDistinct), selector);

        /// <summary>
        /// Observes three properties and combines their values.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, T2, T3, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Func<T1, T2, T3, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, isDistinct)
                     .CombineLatest(sender.WhenAnyValue(property2, isDistinct), (v1, v2) => (v1, v2))
                     .CombineLatest(sender.WhenAnyValue(property3, isDistinct), (pair, v3) => selector(pair.v1, pair.v2, v3));

        /// <summary>
        /// Observes four properties and combines their values.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, T2, T3, T4, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Func<T1, T2, T3, T4, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, isDistinct)
                     .CombineLatest(sender.WhenAnyValue(property2, isDistinct), (v1, v2) => (v1, v2))
                     .CombineLatest(sender.WhenAnyValue(property3, isDistinct), (t, v3) => (t.v1, t.v2, v3))
                     .CombineLatest(sender.WhenAnyValue(property4, isDistinct), (t, v4) => selector(t.v1, t.v2, t.v3, v4));

        /// <summary>
        /// Observes five properties and combines their values.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, T2, T3, T4, T5, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Expression<Func<TSender, T5>> property5,
            Func<T1, T2, T3, T4, T5, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, isDistinct)
                     .CombineLatest(sender.WhenAnyValue(property2, isDistinct), (v1, v2) => (v1, v2))
                     .CombineLatest(sender.WhenAnyValue(property3, isDistinct), (t, v3) => (t.v1, t.v2, v3))
                     .CombineLatest(sender.WhenAnyValue(property4, isDistinct), (t, v4) => (t.v1, t.v2, t.v3, v4))
                     .CombineLatest(sender.WhenAnyValue(property5, isDistinct), (t, v5) => selector(t.v1, t.v2, t.v3, t.v4, v5));

        /// <summary>
        /// Observes six properties and combines their values.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, T2, T3, T4, T5, T6, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Expression<Func<TSender, T5>> property5,
            Expression<Func<TSender, T6>> property6,
            Func<T1, T2, T3, T4, T5, T6, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, isDistinct)
                     .CombineLatest(sender.WhenAnyValue(property2, isDistinct), (v1, v2) => (v1, v2))
                     .CombineLatest(sender.WhenAnyValue(property3, isDistinct), (t, v3) => (t.v1, t.v2, v3))
                     .CombineLatest(sender.WhenAnyValue(property4, isDistinct), (t, v4) => (t.v1, t.v2, t.v3, v4))
                     .CombineLatest(sender.WhenAnyValue(property5, isDistinct), (t, v5) => (t.v1, t.v2, t.v3, t.v4, v5))
                     .CombineLatest(sender.WhenAnyValue(property6, isDistinct), (t, v6) => selector(t.v1, t.v2, t.v3, t.v4, t.v5, v6));

        /// <summary>
        /// Observes seven properties and combines their values.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, T2, T3, T4, T5, T6, T7, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Expression<Func<TSender, T5>> property5,
            Expression<Func<TSender, T6>> property6,
            Expression<Func<TSender, T7>> property7,
            Func<T1, T2, T3, T4, T5, T6, T7, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, isDistinct)
                     .CombineLatest(sender.WhenAnyValue(property2, isDistinct), (v1, v2) => (v1, v2))
                     .CombineLatest(sender.WhenAnyValue(property3, isDistinct), (t, v3) => (t.v1, t.v2, v3))
                     .CombineLatest(sender.WhenAnyValue(property4, isDistinct), (t, v4) => (t.v1, t.v2, t.v3, v4))
                     .CombineLatest(sender.WhenAnyValue(property5, isDistinct), (t, v5) => (t.v1, t.v2, t.v3, t.v4, v5))
                     .CombineLatest(sender.WhenAnyValue(property6, isDistinct), (t, v6) => (t.v1, t.v2, t.v3, t.v4, t.v5, v6))
                     .CombineLatest(sender.WhenAnyValue(property7, isDistinct), (t, v7) => selector(t.v1, t.v2, t.v3, t.v4, t.v5, t.v6, v7));

        /// <summary>
        /// Observes eight properties and combines their values.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, T2, T3, T4, T5, T6, T7, T8, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Expression<Func<TSender, T5>> property5,
            Expression<Func<TSender, T6>> property6,
            Expression<Func<TSender, T7>> property7,
            Expression<Func<TSender, T8>> property8,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, isDistinct)
                     .CombineLatest(sender.WhenAnyValue(property2, isDistinct), (v1, v2) => (v1, v2))
                     .CombineLatest(sender.WhenAnyValue(property3, isDistinct), (t, v3) => (t.v1, t.v2, v3))
                     .CombineLatest(sender.WhenAnyValue(property4, isDistinct), (t, v4) => (t.v1, t.v2, t.v3, v4))
                     .CombineLatest(sender.WhenAnyValue(property5, isDistinct), (t, v5) => (t.v1, t.v2, t.v3, t.v4, v5))
                     .CombineLatest(sender.WhenAnyValue(property6, isDistinct), (t, v6) => (t.v1, t.v2, t.v3, t.v4, t.v5, v6))
                     .CombineLatest(sender.WhenAnyValue(property7, isDistinct), (t, v7) => (t.v1, t.v2, t.v3, t.v4, t.v5, t.v6, v7))
                     .CombineLatest(sender.WhenAnyValue(property8, isDistinct), (t, v8) => selector(t.v1, t.v2, t.v3, t.v4, t.v5, t.v6, t.v7, v8));

        /// <summary>
        /// Observes nine properties and combines their values.
        /// </summary>
        public static IObservable<TRet> WhenAnyValue<TSender, T1, T2, T3, T4, T5, T6, T7, T8, T9, TRet>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Expression<Func<TSender, T5>> property5,
            Expression<Func<TSender, T6>> property6,
            Expression<Func<TSender, T7>> property7,
            Expression<Func<TSender, T8>> property8,
            Expression<Func<TSender, T9>> property9,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TRet> selector,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, isDistinct)
                     .CombineLatest(sender.WhenAnyValue(property2, isDistinct), (v1, v2) => (v1, v2))
                     .CombineLatest(sender.WhenAnyValue(property3, isDistinct), (t, v3) => (t.v1, t.v2, v3))
                     .CombineLatest(sender.WhenAnyValue(property4, isDistinct), (t, v4) => (t.v1, t.v2, t.v3, v4))
                     .CombineLatest(sender.WhenAnyValue(property5, isDistinct), (t, v5) => (t.v1, t.v2, t.v3, t.v4, v5))
                     .CombineLatest(sender.WhenAnyValue(property6, isDistinct), (t, v6) => (t.v1, t.v2, t.v3, t.v4, t.v5, v6))
                     .CombineLatest(sender.WhenAnyValue(property7, isDistinct), (t, v7) => (t.v1, t.v2, t.v3, t.v4, t.v5, t.v6, v7))
                     .CombineLatest(sender.WhenAnyValue(property8, isDistinct), (t, v8) => (t.v1, t.v2, t.v3, t.v4, t.v5, t.v6, t.v7, v8))
                     .CombineLatest(sender.WhenAnyValue(property9, isDistinct), (t, v9) => selector(t.v1, t.v2, t.v3, t.v4, t.v5, t.v6, t.v7, t.v8, v9));

        /// <summary>
        /// Observes two properties and emits a tuple of their values, like ReactiveUI.
        /// </summary>
        public static IObservable<(T1, T2)> WhenAnyValue<TSender, T1, T2>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, property2, static (v1, v2) => (v1, v2), isDistinct);

        /// <summary>
        /// Observes three properties and emits a tuple of their values, like ReactiveUI.
        /// </summary>
        public static IObservable<(T1, T2, T3)> WhenAnyValue<TSender, T1, T2, T3>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, property2, property3, static (v1, v2, v3) => (v1, v2, v3), isDistinct);

        /// <summary>
        /// Observes four properties and emits a tuple of their values, like ReactiveUI.
        /// </summary>
        public static IObservable<(T1, T2, T3, T4)> WhenAnyValue<TSender, T1, T2, T3, T4>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, property2, property3, property4,
                static (v1, v2, v3, v4) => (v1, v2, v3, v4), isDistinct);

        /// <summary>
        /// Observes five properties and emits a tuple of their values, like ReactiveUI.
        /// </summary>
        public static IObservable<(T1, T2, T3, T4, T5)> WhenAnyValue<TSender, T1, T2, T3, T4, T5>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Expression<Func<TSender, T5>> property5,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, property2, property3, property4, property5,
                static (v1, v2, v3, v4, v5) => (v1, v2, v3, v4, v5), isDistinct);

        /// <summary>
        /// Observes six properties and emits a tuple of their values, like ReactiveUI.
        /// </summary>
        public static IObservable<(T1, T2, T3, T4, T5, T6)> WhenAnyValue<TSender, T1, T2, T3, T4, T5, T6>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Expression<Func<TSender, T5>> property5,
            Expression<Func<TSender, T6>> property6,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, property2, property3, property4, property5, property6,
                static (v1, v2, v3, v4, v5, v6) => (v1, v2, v3, v4, v5, v6), isDistinct);

        /// <summary>
        /// Observes seven properties and emits a tuple of their values, like ReactiveUI.
        /// </summary>
        public static IObservable<(T1, T2, T3, T4, T5, T6, T7)> WhenAnyValue<TSender, T1, T2, T3, T4, T5, T6, T7>(this TSender sender,
            Expression<Func<TSender, T1>> property1,
            Expression<Func<TSender, T2>> property2,
            Expression<Func<TSender, T3>> property3,
            Expression<Func<TSender, T4>> property4,
            Expression<Func<TSender, T5>> property5,
            Expression<Func<TSender, T6>> property6,
            Expression<Func<TSender, T7>> property7,
            bool isDistinct = true)
            => sender.WhenAnyValue(property1, property2, property3, property4, property5, property6, property7,
                static (v1, v2, v3, v4, v5, v6, v7) => (v1, v2, v3, v4, v5, v6, v7), isDistinct);

        /// <summary>
        /// Signal "any of the listed properties changed". Carries no data (Unit).
        /// A call with two to seven properties and no selector resolves to the tuple overloads,
        /// so this overload is for eight or more properties when the value itself is not used.
        /// On subscription it emits one signal per property. A notification without a value change produces no signal.
        /// </summary>
        public static IObservable<Unit> WhenAnyValue<TSender>(this TSender sender, params Expression<Func<TSender, object?>>[] properties)
            => sender.WhenAnyValue(true, properties);

        /// <summary>
        /// Signal "any of the listed properties changed", like the overload without isDistinct.
        /// With isDistinct: false the signal fires on every PropertyChanged notification, even without a value change.
        /// The parameter comes first because it cannot be placed after a params array.
        /// </summary>
        public static IObservable<Unit> WhenAnyValue<TSender>(this TSender sender, bool isDistinct, params Expression<Func<TSender, object?>>[] properties)
        {
            return new AnonymousObservable<Unit>(observer =>
            {
                CompositeDisposable subscriptions = new();
                foreach (Expression<Func<TSender, object?>> property in properties)
                {
                    // Subscribes via IObserver: unambiguous and independent of the Subscribe operators.
                    // A getter error is delivered to the subscriber, like the single WhenAnyValue.
                    subscriptions.Add(sender.WhenAnyValue(property, isDistinct)
                        .Subscribe(new AnonymousObserver<object?>(_ => observer.OnNext(Unit.Default), observer.OnError)));
                }

                return subscriptions;
            });
        }

        /// <summary>
        /// Cache of compiled getters per property. PropertyInfo.GetValue is reflection
        /// on every property change for every subscriber; a compiled delegate
        /// is many times cheaper, and compilation happens once per property.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<PropertyInfo, Func<object, object?>> _getters = new();

        /// <summary>
        /// Returns the compiled property getter from the cache.
        /// </summary>
        private static Func<object, object?> GetGetter(PropertyInfo property)
            => _getters.GetOrAdd(property, static p =>
            {
                var instance = Expression.Parameter(typeof(object), "instance");
                var body = Expression.Convert(
                    Expression.Property(Expression.Convert(instance, p.DeclaringType!), p),
                    typeof(object));
                return Expression.Lambda<Func<object, object?>>(body, instance).Compile();
            });

        /// <summary>
        /// Casts the value of the last link to the result type. <see cref="Unwrap"/> strips the cast
        /// from the expression (x =&gt; (double)x.Count), so the getter returns the value in the property type, and a plain
        /// unboxing to another type (int to double) would throw InvalidCastException.
        /// The delegate is compiled once per property type and only when unboxing is not enough.
        /// </summary>
        private static class ValueConverter<TRet>
        {
            private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<object, TRet>?> _cache = new();

            public static Func<object, TRet>? For(Type source)
                => _cache.GetOrAdd(source, static type =>
                {
                    if (typeof(TRet).IsAssignableFrom(type) || Nullable.GetUnderlyingType(typeof(TRet)) == type)
                        return null;

                    try
                    {
                        var value = Expression.Parameter(typeof(object), "value");
                        var body = Expression.Convert(Expression.Convert(value, type), typeof(TRet));
                        return Expression.Lambda<Func<object, TRet>>(body, value).Compile();
                    }
                    catch (InvalidOperationException)
                    {
                        // No cast between the types: plain unboxing remains, as before.
                        return null;
                    }
                });
        }

        private static PropertyInfo[] GetPropertyChain(Expression body)
        {
            List<PropertyInfo> list = new();
            Expression? expr = Unwrap(body);
            while (expr is MemberExpression member && member.Member is PropertyInfo property)
            {
                list.Add(property);
                expr = Unwrap(member.Expression);
            }

            if (expr is not ParameterExpression)
                throw new ArgumentException("A property access expression is expected, for example x => x.A.B.", nameof(body));

            list.Reverse();
            return list.ToArray();
        }

        // Strips type-cast wrappers (x => (object)x.Prop) from a property access expression.
        internal static Expression? Unwrap(Expression? expression)
        {
            while (expression is UnaryExpression unary &&
                   (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unary.Operand;
            }

            return expression;
        }

        /// <summary>
        /// Tracks a property chain: subscribes to each level and resubscribes below on changes.
        /// With isDistinct it does not emit a value equal to the previously emitted one.
        /// </summary>
        private sealed class PropertyChainObserver<TRet> : IDisposable
        {
            private readonly object _root;
            private readonly PropertyInfo[] _chain;
            private readonly Func<object, TRet>? _convert;
            private readonly bool _isDistinct;
            private readonly IObserver<TRet> _observer;
            private readonly object?[] _instances;
            private readonly PropertyChangedEventHandler?[] _handlers;
            private readonly object _gate = new();
            private bool _disposed;
            private bool _hasValue;
            private TRet _lastValue = default!;

            public PropertyChainObserver(object root, PropertyInfo[] chain, Func<object, TRet>? convert, bool isDistinct, IObserver<TRet> observer)
            {
                _root = root;
                _chain = chain;
                _convert = convert;
                _isDistinct = isDistinct;
                _observer = observer;
                _instances = new object?[chain.Length];
                _handlers = new PropertyChangedEventHandler?[chain.Length];
            }

            public void Start() => Emit(0);

            // Reattaches handlers and recomputes instances starting from the specified level.
            private void BuildFrom(int level)
            {
                for (int i = level; i < _chain.Length; i++)
                    DetachHandler(i);

                for (int i = level; i < _chain.Length; i++)
                {
                    object? instance = i == 0
                        ? _root
                        : (_instances[i - 1] is null ? null : GetGetter(_chain[i - 1])(_instances[i - 1]!));
                    _instances[i] = instance;

                    if (instance is INotifyPropertyChanged notify)
                    {
                        int captured = i;
                        PropertyChangedEventHandler handler = (_, e) => OnPropertyChanged(captured, e);
                        _handlers[i] = handler;
                        notify.PropertyChanged += handler;
                    }
                }
            }

            private void DetachHandler(int level)
            {
                if (_handlers[level] is { } handler && _instances[level] is INotifyPropertyChanged notify)
                    notify.PropertyChanged -= handler;

                _handlers[level] = null;
            }

            private void OnPropertyChanged(int level, PropertyChangedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != _chain[level].Name)
                    return;

                Emit(level + 1);
            }

            // Reattaches handlers from level rebuildFrom and emits the current value.
            // Attaching and reading under one lock: otherwise a notification from another thread between them
            // would record the value first, and the subscription would not get the initial value on its own thread.
            private void Emit(int rebuildFrom)
            {
                TRet value;
                Exception? error = null;
                lock (_gate)
                {
                    if (_disposed)
                        return;

                    BuildFrom(rebuildFrom);

                    // An exception from a chain property getter or from the value's Equals is delivered to the subscriber
                    // as OnError rather than propagated into the code that changed the property (usually a foreign model setter).
                    try
                    {
                        value = GetCurrentValue();
                        // The previous value is not re-emitted, like ReactiveUI: a notification without a value change
                        // (RaisePropertyChanged, replacing an intermediate link with an object of the same value) is silent.
                        // Comparison under the lock: two concurrent notifications will not emit the same value twice.
                        if (_isDistinct && _hasValue && EqualityComparer<TRet>.Default.Equals(_lastValue, value))
                            return;
                    }
                    catch (Exception ex) { error = ex; value = default!; }

                    // The value is recorded before emission: if the subscriber changes the property in OnNext
                    // (clamps or rolls back), the new value is compared with the one just emitted.
                    if (error is null && _isDistinct)
                    {
                        _hasValue = true;
                        _lastValue = value;
                    }
                }

                if (error is not null)
                    _observer.OnError(error);
                else
                    _observer.OnNext(value);
            }

            private TRet GetCurrentValue()
            {
                object? current = _root;
                for (int i = 0; i < _chain.Length; i++)
                {
                    if (current is null)
                        return default!;

                    current = GetGetter(_chain[i])(current);
                }

                if (current is null)
                    return default!;

                return _convert is null ? (TRet)current : _convert(current);
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed)
                        return;

                    _disposed = true;
                    for (int i = 0; i < _chain.Length; i++)
                        DetachHandler(i);

                    // A subscription may outlive its disposal (held in a container): do not retain the last value.
                    _lastValue = default!;
                }
            }
        }
    }
}