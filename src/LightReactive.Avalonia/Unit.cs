namespace LightReactive.Avalonia
{
    /// <summary>
    /// Type with a single value. Replacement for System.Reactive.Unit.
    /// Used as the result of commands and of observables without meaningful data.
    /// </summary>
    public readonly struct Unit : IEquatable<Unit>
    {
        /// <summary>
        /// The single value of the type.
        /// </summary>
        public static Unit Default => default;

        /// <inheritdoc />
        public bool Equals(Unit other) => true;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Unit;

        /// <inheritdoc />
        public override int GetHashCode() => 0;

        /// <inheritdoc />
        public override string ToString() => "()";

        /// <summary>
        /// All values are equal.
        /// </summary>
        public static bool operator ==(Unit left, Unit right) => true;

        /// <summary>
        /// All values are equal.
        /// </summary>
        public static bool operator !=(Unit left, Unit right) => false;
    }
}
