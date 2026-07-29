#if (IL2CPPMELON)
using S1Product = Il2CppScheduleOne.Product;
using S1Registry = Il2CppScheduleOne.Registry;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Product = ScheduleOne.Product;
using S1Registry = ScheduleOne.Registry;
#endif
using System;
using S1API.Internal.Utils;

namespace S1API.Items.Ingredient
{
    /// <summary>
    /// Provides convenient static methods for creating custom mixing ingredients.
    /// Use <see cref="CreateBuilder"/> for creating ingredients from scratch, or <see cref="CloneFrom(string)"/> for variants.
    /// </summary>
    public static class MixIngredientItemCreator
    {
        /// <summary>
        /// Creates a new builder for composing a mixing ingredient definition with full flexibility.
        /// Use fluent methods to configure the ingredient, then call Build() to register it.
        /// </summary>
        /// <returns>A new <see cref="MixIngredientDefinitionBuilder"/> instance for fluent configuration.</returns>
        public static MixIngredientDefinitionBuilder CreateBuilder()
        {
            return new MixIngredientDefinitionBuilder();
        }

        /// <summary>
        /// Creates a new ingredient builder by cloning an existing mixing ingredient by ID.
        /// </summary>
        /// <param name="sourceItemId">The ID of the ingredient to clone.</param>
        /// <returns>A builder pre-configured with the source ingredient's properties.</returns>
        /// <exception cref="ArgumentException">Thrown if the source item does not exist or is not a mixing ingredient.</exception>
        public static MixIngredientDefinitionBuilder CloneFrom(string sourceItemId)
        {
            if (string.IsNullOrWhiteSpace(sourceItemId))
            {
                throw new ArgumentException("Source item ID cannot be null or whitespace", nameof(sourceItemId));
            }

            var sourceDefinition = S1Registry.GetItem(sourceItemId);
            if (sourceDefinition == null)
            {
                throw new ArgumentException($"Source item with ID '{sourceItemId}' not found in registry", nameof(sourceItemId));
            }

            if (!CrossType.Is(sourceDefinition, out S1Product.PropertyItemDefinition propertyDef))
            {
                throw new ArgumentException($"Item '{sourceItemId}' is not a PropertyItemDefinition and cannot be used as a mixing ingredient", nameof(sourceItemId));
            }

            return new MixIngredientDefinitionBuilder(propertyDef);
        }

        /// <summary>
        /// Creates a new ingredient builder by cloning an existing ingredient wrapper.
        /// </summary>
        /// <param name="source">The ingredient definition to clone from.</param>
        /// <returns>A builder pre-configured with the source ingredient's properties.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the source is null.</exception>
        public static MixIngredientDefinitionBuilder CloneFrom(MixIngredientDefinition source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source), "Source ingredient definition cannot be null");
            }

            return new MixIngredientDefinitionBuilder(source.S1IngredientDefinition);
        }
    }
}
