#if (IL2CPPMELON)
using S1Properties = Il2CppScheduleOne.Effects;
using S1MixMaps = Il2CppScheduleOne.Effects.MixMaps;
using S1Product = Il2CppScheduleOne.Product;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Properties = ScheduleOne.Effects;
using S1MixMaps = ScheduleOne.Effects.MixMaps;
using S1Product = ScheduleOne.Product;
#endif
using System;
using System.Collections.Generic;
#if (MONOMELON || MONOBEPINEX)
using S1API.Internal.Utils;
#endif
using S1API.Lifecycle;
using S1API.Logging;
using UnityEngine;

namespace S1API.Internal.Properties
{
    /// <summary>
    /// INTERNAL: Tracks mod-created effects so they can be (re)applied to the game each load.
    /// Effects are injected into <c>PropertyUtility</c> (both its list and its ID lookup) and given a position
    /// on each relevant per-drug MixerMap so they survive being mixed again without crashing the game.
    /// </summary>
    internal static class CustomEffectRegistry
    {
        private sealed class Entry
        {
            internal S1Properties.Effect S1Effect { get; set; } = null!;
            internal S1Product.EDrugType[] S1Drugs { get; set; } = Array.Empty<S1Product.EDrugType>();
            internal Vector2? MixMapPosition { get; set; }
            internal float MixMapRadius { get; set; }
        }

        private static readonly Log Logger = new Log("CustomEffectRegistry");
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Entry> Effects =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static bool _hooked;

        /// <summary>
        /// Records a custom effect and its mixing placement, and applies it immediately if the game is running.
        /// </summary>
        internal static void Register(
            S1Properties.Effect effect,
            S1Product.EDrugType[] drugs,
            Vector2? mixMapPosition,
            float mixMapRadius)
        {
            if (effect == null)
                return;

            var id = effect.ID;
            if (string.IsNullOrWhiteSpace(id))
            {
                Logger.Warning("Custom effect has no ID; it will not be tracked.");
                return;
            }

            var entry = new Entry
            {
                S1Effect = effect,
                S1Drugs = drugs ?? Array.Empty<S1Product.EDrugType>(),
                MixMapPosition = mixMapPosition,
                MixMapRadius = mixMapRadius
            };

            lock (Gate)
            {
                Effects[id] = entry;
                EnsureHooked();
            }

            Apply(entry);
        }

        /// <summary>
        /// Returns the effect already registered under the given ID, if any. Used to keep repeated builds of
        /// the same ID idempotent instead of injecting a second object the game's lookups would not resolve to.
        /// </summary>
        internal static bool TryGetExisting(string id, out S1Properties.Effect effect)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                effect = null!;
                return false;
            }

            lock (Gate)
            {
                if (Effects.TryGetValue(id, out var entry))
                {
                    effect = entry.S1Effect;
                    return true;
                }
            }

            effect = null!;
            return false;
        }

        private static void EnsureHooked()
        {
            if (_hooked)
                return;

            // Apply on pre-load so saved products that reference a custom effect can resolve its ID during load,
            // and again on load-complete once the mixing systems (MixerMaps) are available.
            GameLifecycle.OnPreLoad += ApplyAll;
            GameLifecycle.OnLoadComplete += ApplyAll;
            _hooked = true;
        }

        private static void ApplyAll()
        {
            foreach (var entry in Snapshot())
                Apply(entry);
        }

        private static Entry[] Snapshot()
        {
            lock (Gate)
            {
                var snapshot = new Entry[Effects.Count];
                Effects.Values.CopyTo(snapshot, 0);
                return snapshot;
            }
        }

        private static void Apply(Entry entry)
        {
            EnsureInPropertyUtility(entry);
            EnsureInMixMaps(entry);
        }

        private static void EnsureInPropertyUtility(Entry entry)
        {
            if (entry?.S1Effect == null || string.IsNullOrWhiteSpace(entry.S1Effect.ID))
                return;

            try
            {
                var propertyUtility = S1Product.PropertyUtility.Instance;
                if (propertyUtility == null)
                    return;

                var allProperties = propertyUtility.AllProperties;
                if (allProperties == null)
                    return;

                var effect = entry.S1Effect;

                // Always keep the ID lookup consistent with the list; the game's GetProperties(List<string>)
                // reads the list to test membership and then indexes the dictionary, so both must contain the ID.
                var inDictionary = EnsureInPropertiesDict(propertyUtility, effect);

                var inList = false;
                for (int i = 0; i < allProperties.Count; i++)
                {
                    var existing = allProperties[i];
                    if (existing != null &&
                        string.Equals(existing.ID, effect.ID, StringComparison.OrdinalIgnoreCase))
                    {
                        inList = true;
                        break;
                    }
                }

                if (!inList && inDictionary)
                    allProperties.Add(effect);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add custom effect '{entry.S1Effect?.ID}' to PropertyUtility: {ex}");
            }
        }

        private static bool EnsureInPropertiesDict(S1Product.PropertyUtility propertyUtility, S1Properties.Effect effect)
        {
#if (IL2CPPMELON || IL2CPPBEPINEX)
            var dict = propertyUtility.PropertiesDict;
            if (dict == null)
                return false;
            if (!dict.ContainsKey(effect.ID))
                dict.Add(effect.ID, effect);
            return true;
#else
            var dictObject = ReflectionUtils.TryGetFieldOrProperty(propertyUtility, "PropertiesDict");
            if (dictObject is System.Collections.Generic.Dictionary<string, S1Properties.Effect> dict)
            {
                if (!dict.ContainsKey(effect.ID))
                    dict.Add(effect.ID, effect);
                return true;
            }
            return false;
#endif
        }

        private static void EnsureInMixMaps(Entry entry)
        {
            if (entry?.S1Effect == null || entry.S1Drugs == null)
                return;

            try
            {
                var productManager = S1Product.ProductManager.Instance;
                if (productManager == null)
                    return;

                foreach (var drug in entry.S1Drugs)
                {
                    var map = productManager.GetMixerMap(drug);
                    if (map == null || map.Effects == null)
                        continue;

                    if (ContainsEffect(map, entry.S1Effect.ID))
                        continue;

                    var mixerMapEffect = new S1MixMaps.MixerMapEffect();
                    mixerMapEffect.Property = entry.S1Effect;
                    mixerMapEffect.Position = entry.MixMapPosition ?? DefaultPosition(entry.S1Effect, map.MapRadius);
                    mixerMapEffect.Radius = entry.MixMapRadius > 0f ? entry.MixMapRadius : 0.01f;

                    map.Effects.Add(mixerMapEffect);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to place custom effect '{entry.S1Effect?.ID}' on a MixerMap: {ex}");
            }
        }

        private static bool ContainsEffect(S1MixMaps.MixerMap map, string effectId)
        {
            for (int i = 0; i < map.Effects.Count; i++)
            {
                var existing = map.Effects[i];
                if (existing?.Property != null &&
                    string.Equals(existing.Property.ID, effectId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Places the effect just outside the map so it never captures an incoming mix by default,
        /// while still giving it a valid position so re-mixing does not throw.
        /// </summary>
        private static Vector2 DefaultPosition(S1Properties.Effect effect, float mapRadius)
        {
            var hash = (effect.ID ?? string.Empty).GetHashCode() & 0x7fffffff;
            var angle = (hash % 360) * Mathf.Deg2Rad;
            var radius = (mapRadius > 0f ? mapRadius : 5f) + 1f;
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }
}
