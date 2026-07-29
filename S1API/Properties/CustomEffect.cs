#if (IL2CPPMELON)
using S1Properties = Il2CppScheduleOne.Effects;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Properties = ScheduleOne.Effects;
#endif

namespace S1API.Properties
{
    /// <summary>
    /// A property/effect created at runtime by a mod.
    /// Behaves like any other property token and can be passed anywhere a vanilla
    /// <see cref="Property"/> token is accepted (e.g. a mixing ingredient's effect).
    /// </summary>
    public sealed class CustomEffect : ProductPropertyWrapper
    {
        /// <summary>
        /// INTERNAL: Wraps the created native effect.
        /// </summary>
        internal CustomEffect(S1Properties.Effect effect)
            : base(effect)
        {
        }
    }
}
