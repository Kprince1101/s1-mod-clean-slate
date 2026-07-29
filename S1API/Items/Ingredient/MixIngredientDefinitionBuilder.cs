#if (IL2CPPMELON)
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
using S1CoreItemFramework = Il2CppScheduleOne.Core.Items.Framework;
using S1Product = Il2CppScheduleOne.Product;
using S1Properties = Il2CppScheduleOne.Effects;
using S1Registry = Il2CppScheduleOne.Registry;
using S1Storage = Il2CppScheduleOne.Storage;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1ItemFramework = ScheduleOne.ItemFramework;
using S1CoreItemFramework = ScheduleOne.Core.Items.Framework;
using S1Product = ScheduleOne.Product;
using S1Properties = ScheduleOne.Effects;
using S1Registry = ScheduleOne.Registry;
using S1Storage = ScheduleOne.Storage;
#endif
using System;
using System.Collections.Generic;
using S1API.Internal.Items;
using S1API.Internal.Properties;
using S1API.Internal.Utils;
using S1API.Items.Storable;
using S1API.Logging;
using S1API.Properties.Interfaces;
using UnityEngine;

namespace S1API.Items.Ingredient
{
    /// <summary>
    /// Builder for composing mixing ingredient definitions at runtime.
    /// Use fluent methods to configure the ingredient and its imprinted effects before calling <see cref="Build"/>.
    /// </summary>
    public sealed class MixIngredientDefinitionBuilder
        : StorableItemDefinitionBuilderBase<MixIngredientDefinitionBuilder>
    {
        private static readonly Log Logger = new Log("MixIngredientDefinitionBuilder");

        private static readonly string[] MixerTemplateIds =
        {
            "iodine", "cuke", "banana", "donut", "chili", "paracetamol", "energydrink",
            "motoroil", "mouthwash", "megabean", "battery", "addy", "flumedicine", "gasoline"
        };

        private S1Product.PropertyItemDefinition IngredientDefinition =>
            CrossType.As<S1Product.PropertyItemDefinition>(Definition);

        /// <summary>
        /// INTERNAL: Creates a new builder instance with a fresh PropertyItemDefinition.
        /// Only <see cref="MixIngredientItemCreator"/> can instantiate this.
        /// </summary>
        internal MixIngredientDefinitionBuilder()
            : base(ScriptableObject.CreateInstance<S1Product.PropertyItemDefinition>)
        {
            Definition.Category = (S1CoreItemFramework.EItemCategory)ItemCategory.Ingredient;
        }

        /// <summary>
        /// INTERNAL: Creates a builder instance initialized by cloning an existing ingredient.
        /// </summary>
        internal MixIngredientDefinitionBuilder(
            S1Product.PropertyItemDefinition source)
            : base(source,
                ScriptableObject.CreateInstance<S1Product.PropertyItemDefinition>)
        {
        }

        /// <inheritdoc/>
        protected override void CopyPropertiesFrom(
            S1ItemFramework.StorableItemDefinition source)
        {
            base.CopyPropertiesFrom(source);

            var ingredientSource = CrossType.As<S1Product.PropertyItemDefinition>(source);
            if (ingredientSource == null)
                return;

            var copy = new List<S1Properties.Effect>();
            var sourceProperties = ingredientSource.Properties;
            if (sourceProperties != null)
            {
                for (int i = 0; i < sourceProperties.Count; i++)
                    copy.Add(sourceProperties[i]);
            }

            IngredientDefinition.Properties = ToIl2CppList(copy);
        }

        /// <summary>
        /// Sets the single effect this ingredient imprints when mixed.
        /// </summary>
        /// <param name="effect">The effect to imprint (a vanilla token from <c>S1API.Properties.Property</c> or a registered custom effect).</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public MixIngredientDefinitionBuilder WithEffect(PropertyBase effect)
        {
            return WithEffects(effect);
        }

        /// <summary>
        /// Sets the effects this ingredient carries. The first effect is the one applied to the product when mixed.
        /// </summary>
        /// <param name="effects">The effects to carry (vanilla tokens or registered custom effects).</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public MixIngredientDefinitionBuilder WithEffects(params PropertyBase[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                Logger.Warning("WithEffects called with no effects; the ingredient will not imprint anything when mixed.");
                return this;
            }

            var resolved = PropertyResolver.ResolveToGameProperties(effects);
            if (resolved.Count == 0)
            {
                Logger.Warning(
                    "None of the supplied effects resolved to a game effect. Use a vanilla token or a registered custom effect.");
            }

            IngredientDefinition.Properties = ToIl2CppList(resolved);
            return this;
        }

        /// <summary>
        /// Builds the ingredient definition, registers it with the game's registry and the mixing-ingredient
        /// list, and returns a wrapper. The ingredient is automatically re-applied on subsequent loads.
        /// </summary>
        /// <returns>A wrapper around the created ingredient definition.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the ingredient carries no resolved effects.</exception>
        public new MixIngredientDefinition Build()
        {
            // Building the same ID again (e.g. from a per-load setup hook) reuses the first registration
            // instead of adding a duplicate mixing ingredient.
            var id = Definition.ID;
            if (CustomIngredientRegistry.TryGetExisting(id, out var existing))
            {
                Logger.Warning($"Mixing ingredient '{id}' is already registered; returning the existing one.");
                return new MixIngredientDefinition(existing);
            }

            var properties = IngredientDefinition.Properties;
            if (properties == null || properties.Count == 0)
            {
                Logger.Error(
                    "Cannot build mixing ingredient: it carries no effects. Use WithEffect(...) with a vanilla token or a registered custom effect.");
                throw new InvalidOperationException("Cannot build mixing ingredient: at least one effect is required.");
            }

            EnsureStoredItem();
            var built = (MixIngredientDefinition)base.Build();
            CustomIngredientRegistry.Register(IngredientDefinition);
            return built;
        }

        /// <summary>
        /// Gives the ingredient a working StoredItem. The base builder only attaches a bare placeholder, which
        /// has no footprint tiles, so the game throws when it places the ingredient into a slot. A real mixing
        /// ingredient's StoredItem (e.g. Iodine's) has a valid footprint and model, so reuse one of those.
        /// </summary>
        private void EnsureStoredItem()
        {
            // Respect a StoredItem the modder supplied via WithStoredItem; only fill in a default otherwise.
            if (HasCustomStoredItem)
                return;

            var template = FindMixerStoredItem();
            if (template != null)
            {
                Definition.StoredItem = template;
                return;
            }

            // Fallback if no vanilla mixer is available: give the placeholder a 1x1 footprint so it does not throw.
            var storedItem = Definition.StoredItem;
            if (storedItem == null)
                return;

            ReflectionUtils.TrySetFieldOrProperty(storedItem, "footprintX", 1);
            ReflectionUtils.TrySetFieldOrProperty(storedItem, "footprintY", 1);
        }

        private static S1Storage.StoredItem FindMixerStoredItem()
        {
            foreach (var id in MixerTemplateIds)
            {
                var definition = S1Registry.GetItem(id);
                if (definition == null)
                    continue;

                if (CrossType.Is(definition, out S1ItemFramework.StorableItemDefinition storable) &&
                    storable.StoredItem != null)
                    return storable.StoredItem;
            }

            return null;
        }

        /// <summary>
        /// INTERNAL: Builds and returns the raw game item definition without registering.
        /// Used internally by S1API. Modders should use <see cref="Build"/> instead.
        /// </summary>
        internal new S1Product.PropertyItemDefinition BuildInternal()
        {
            return IngredientDefinition;
        }

        /// <inheritdoc />
        protected override Storable.StorableItemDefinition CreateWrapper(
            S1ItemFramework.StorableItemDefinition definition)
        {
            return new MixIngredientDefinition(CrossType.As<S1Product.PropertyItemDefinition>(definition));
        }

#if (IL2CPPMELON || IL2CPPBEPINEX)
        private static Il2CppSystem.Collections.Generic.List<T> ToIl2CppList<T>(List<T> source)
        {
            var list = new Il2CppSystem.Collections.Generic.List<T>();
            if (source == null)
                return list;
            for (int i = 0; i < source.Count; i++)
                list.Add(source[i]);
            return list;
        }
#else
        private static List<T> ToIl2CppList<T>(List<T> source)
        {
            return source;
        }
#endif
    }
}
