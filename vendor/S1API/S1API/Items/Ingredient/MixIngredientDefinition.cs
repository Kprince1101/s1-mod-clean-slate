#if (IL2CPPMELON)
using S1Product = Il2CppScheduleOne.Product;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Product = ScheduleOne.Product;
#endif
using System.Collections.Generic;
using S1API.Properties;
using S1API.Properties.Interfaces;

namespace S1API.Items.Ingredient
{
    /// <summary>
    /// Represents a mixing ingredient item definition.
    /// A mixing ingredient is a <see cref="Storable.StorableItemDefinition"/> that carries one or more
    /// effects and can be dropped into the Mixing Station to imprint those effects onto a product.
    /// </summary>
    /// <remarks>
    /// Builder-only: the effect list is intentionally read-only here to avoid runtime surprises from
    /// mutating globally-registered ScriptableObject definitions mid-session. Use
    /// <see cref="MixIngredientItemCreator"/> to create ingredients with configured effects.
    /// </remarks>
    public sealed class MixIngredientDefinition : Storable.StorableItemDefinition
    {
        /// <summary>
        /// INTERNAL: Wraps an existing native property item definition used as a mixing ingredient.
        /// </summary>
        internal MixIngredientDefinition(S1Product.PropertyItemDefinition definition)
            : base(definition)
        {
            S1IngredientDefinition = definition;
        }

        /// <summary>
        /// INTERNAL: A reference to the native game property item definition.
        /// </summary>
        internal S1Product.PropertyItemDefinition S1IngredientDefinition { get; }

        /// <summary>
        /// The effects this ingredient imprints when mixed.
        /// The first effect is the one applied to the product; further effects are carried on the item.
        /// Returns runtime-agnostic wrappers that work on both Mono and IL2CPP.
        /// </summary>
        public IReadOnlyList<PropertyBase> Properties
        {
            get
            {
                var s1Properties = S1IngredientDefinition.Properties;
                if (s1Properties == null)
                    return new List<PropertyBase>().AsReadOnly();

                var wrappers = new List<PropertyBase>(s1Properties.Count);
                for (int i = 0; i < s1Properties.Count; i++)
                    wrappers.Add(new ProductPropertyWrapper(s1Properties[i]));

                return wrappers.AsReadOnly();
            }
        }
    }
}
