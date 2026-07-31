using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Storage;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Delivery
{
    // Which of the player's owned properties currently have a GRQD pickup dock, and the
    // live PickupDock instances backing them. Enabled set persists via LegionCore.Api.Save
    // (MelonPreferences-backed) as a comma-joined list of PropertyCode - simple and
    // survives a save reload without needing its own save-file format.
    public static class DockRegistry
    {
        private const string PrefKey = "GRQD_EnabledDockPropertyCodes";
        private static readonly Dictionary<string, PickupDock> Live = new();
        private static bool _typeRegistered;

        private static void EnsureTypeRegistered()
        {
            if (_typeRegistered) return;
            _typeRegistered = true;
            ClassInjector.RegisterTypeInIl2Cpp<PickupDock>();
        }

        // All owned properties/businesses that have at least one StorageEntity - the set a
        // player could plausibly want GRQD to pick up from. Mirrors the union pattern vanilla
        // itself uses in PropertyManager.GetNearestProperty (Property.OwnedProperties and
        // Business.OwnedBusinesses are separate, non-overlapping lists).
        public static List<Property> GetEligibleProperties()
        {
            // Property.OwnedProperties / Business.OwnedBusinesses are Il2Cpp-backed lists, not
            // real C# IEnumerable<T> - can't AddRange/Cast/foreach them directly (same interop
            // gotcha class as the Transform-foreach bug). Index into them explicitly instead.
            var all = new List<Property>();
            var owned = Property.OwnedProperties;
            for (int i = 0; i < owned.Count; i++) all.Add(owned[i]);
            var businesses = Business.OwnedBusinesses;
            for (int i = 0; i < businesses.Count; i++) all.Add(businesses[i]);

            return all.Where(p => p != null && p.GetComponentsInChildren<StorageEntity>(true).Length > 0)
                .Distinct()
                .ToList();
        }

        public static bool IsEnabled(string propertyCode) => GetEnabledCodes().Contains(propertyCode);

        // Exposes the live dock instance for a property, if its dock is currently enabled -
        // lets a route spawn its van at the dock's actual position instead of recomputing the
        // same fixed offset a second time (see RouteManager.OnRouteDue).
        public static bool TryGetDock(string propertyCode, out PickupDock? dock) => Live.TryGetValue(propertyCode, out dock);

        public static void SetEnabled(string propertyCode, bool enabled)
        {
            var codes = GetEnabledCodes();
            if (enabled) codes.Add(propertyCode);
            else codes.Remove(propertyCode);
            LegionCore.Api.Save.SetString(PrefKey, string.Join(",", codes));

            if (enabled) SpawnIfNeeded(propertyCode);
            else DespawnIfPresent(propertyCode);
        }

        // Call once readiness is confirmed (game loaded) - spawns docks for whatever was
        // enabled in a prior session.
        public static void RestoreFromSave()
        {
            foreach (var code in GetEnabledCodes())
                SpawnIfNeeded(code);
        }

        private static HashSet<string> GetEnabledCodes()
        {
            var raw = LegionCore.Api.Save.GetString(PrefKey, string.Empty);
            return string.IsNullOrEmpty(raw)
                ? new HashSet<string>()
                : new HashSet<string>(raw.Split(','));
        }

        private static void SpawnIfNeeded(string propertyCode)
        {
            if (Live.ContainsKey(propertyCode)) return;

            var property = GetEligibleProperties().FirstOrDefault(p => p.PropertyCode == propertyCode);
            if (property == null)
            {
                MelonLogger.Warning($"LegionCore: DockRegistry could not find property '{propertyCode}' to spawn a dock on.");
                return;
            }

            EnsureTypeRegistered();

            // Arbitrary fixed offset from the property's own transform - exact positioning
            // isn't load-bearing per grqd-spec.md ("an arbitrary workable point per property
            // is fine for v1, not worth blocking on. Player doesn't place it in v1.").
            var go = new GameObject($"GRQD_PickupDock_{propertyCode}");
            go.transform.position = property.transform.position + new Vector3(3f, 0f, 3f);
            var dock = go.AddComponent<PickupDock>();
            dock.PropertyCode = propertyCode;
            Live[propertyCode] = dock;

            MelonLogger.Msg($"LegionCore: pickup dock enabled at '{property.PropertyName}' ({propertyCode}).");
        }

        private static void DespawnIfPresent(string propertyCode)
        {
            if (!Live.TryGetValue(propertyCode, out var dock)) return;
            Live.Remove(propertyCode);
            if (dock != null) UnityEngine.Object.Destroy(dock.gameObject);
        }
    }
}
