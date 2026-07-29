namespace S1API.Properties
{
    /// <summary>
    /// Provides convenient static methods for creating custom effects (drug properties).
    /// </summary>
    public static class EffectCreator
    {
        /// <summary>
        /// Creates a new builder for composing a custom effect.
        /// Use fluent methods to configure the effect, then call Build() to register it.
        /// </summary>
        /// <returns>A new <see cref="CustomEffectBuilder"/> instance for fluent configuration.</returns>
        public static CustomEffectBuilder CreateBuilder()
        {
            return new CustomEffectBuilder();
        }
    }
}
