#if (IL2CPPMELON)
using S1Product = Il2CppScheduleOne.Product;
using S1Registry = Il2CppScheduleOne.Registry;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Product = ScheduleOne.Product;
using S1Registry = ScheduleOne.Registry;
#endif
using System;
using System.Collections.Generic;
using S1API.Lifecycle;
using S1API.Logging;

namespace S1API.Internal.Items
{
    /// <summary>
    /// INTERNAL: Tracks mod-created mixing ingredients and re-applies them across scene changes.
    /// The game's item registry is wiped on scene change, and the mixing-ingredient list lives on a
    /// session-scoped singleton, so both must be re-populated on every load.
    /// </summary>
    internal static class CustomIngredientRegistry
    {
        private static readonly Log Logger = new Log("CustomIngredientRegistry");
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, S1Product.PropertyItemDefinition> Ingredients =
            new Dictionary<string, S1Product.PropertyItemDefinition>(StringComparer.OrdinalIgnoreCase);
        private static bool _hooked;

        /// <summary>
        /// Records an ingredient so it is re-applied on future loads, and applies it to the mixing-ingredient
        /// list immediately if the game is already running.
        /// </summary>
        internal static void Register(S1Product.PropertyItemDefinition definition)
        {
            if (definition == null)
                return;

            var id = definition.ID;
            if (string.IsNullOrWhiteSpace(id))
            {
                Logger.Warning("Ingredient has no ID; it will not be tracked as a mixing ingredient.");
                return;
            }

            lock (Gate)
            {
                Ingredients[id] = definition;
                EnsureHooked();
            }

            EnsureInMixIngredients(definition);
        }

        /// <summary>
        /// Returns the ingredient already registered under the given ID, if any. Used to keep repeated
        /// builds of the same ID idempotent instead of registering duplicate definitions.
        /// </summary>
        internal static bool TryGetExisting(string id, out S1Product.PropertyItemDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            lock (Gate)
            {
                return Ingredients.TryGetValue(id, out definition);
            }
        }

        private static void EnsureHooked()
        {
            if (_hooked)
                return;

            GameLifecycle.OnPreLoad += OnPreLoad;
            GameLifecycle.OnLoadComplete += OnLoadComplete;
            _hooked = true;
        }

        private static void OnPreLoad()
        {
            ReapplyRegistry();
        }

        private static void OnLoadComplete()
        {
            ReapplyRegistry();
            ReapplyMixIngredients();
        }

        private static S1Product.PropertyItemDefinition[] Snapshot()
        {
            lock (Gate)
            {
                var snapshot = new S1Product.PropertyItemDefinition[Ingredients.Count];
                Ingredients.Values.CopyTo(snapshot, 0);
                return snapshot;
            }
        }

        private static void ReapplyRegistry()
        {
            if (S1Registry.Instance == null)
                return;

            foreach (var definition in Snapshot())
            {
                if (definition == null)
                    continue;

                try
                {
                    if (!S1Registry.ItemExists(definition.ID))
                        S1Registry.Instance.AddToRegistry(definition);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to re-register ingredient '{definition.ID}': {ex}");
                }
            }
        }

        private static void ReapplyMixIngredients()
        {
            foreach (var definition in Snapshot())
            {
                if (definition == null)
                    continue;

                EnsureInMixIngredients(definition);
            }
        }

        private static bool EnsureInMixIngredients(S1Product.PropertyItemDefinition definition)
        {
            try
            {
                var productManager = S1Product.ProductManager.Instance;
                if (productManager == null)
                    return false;

                var validMixIngredients = productManager.ValidMixIngredients;
                if (validMixIngredients == null)
                    return false;

                if (!validMixIngredients.Contains(definition))
                    validMixIngredients.Add(definition);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add ingredient '{definition?.ID}' to the mixing-ingredient list: {ex}");
                return false;
            }
        }
    }
}
