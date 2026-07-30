using System.Collections.Generic;
using System.Linq;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Storage;

namespace LegionCore.Delivery
{
    // The single locker GRQD's driver picks product up from. One assignment for now (the
    // spec's "pay locker" concept - name kept generic here since it's really just "the
    // locker the driver works out of"). Persisted as "PropertyCode::indexWithinProperty"
    // since StorageEntity has no stable save-safe GUID exposed - re-resolved by re-scanning
    // the property's storages in the same order every time (GetComponentsInChildren order is
    // stable for a given scene/session).
    public static class LockerRegistry
    {
        private const string PrefKey = "GRQD_AssignedLockerKey";

        public readonly struct LockerOption
        {
            public readonly string Key;
            public readonly string PropertyName;
            public readonly StorageEntity Storage;

            public LockerOption(string key, string propertyName, StorageEntity storage)
            {
                Key = key;
                PropertyName = propertyName;
                Storage = storage;
            }
        }

        // Every StorageEntity across every owned property/business, each with a stable key.
        public static List<LockerOption> GetEligibleLockers()
        {
            var options = new List<LockerOption>();
            foreach (var property in DockRegistry.GetEligibleProperties())
            {
                var storages = property.GetComponentsInChildren<StorageEntity>(true);
                for (int i = 0; i < storages.Length; i++)
                {
                    var key = property.PropertyCode + "::" + i;
                    options.Add(new LockerOption(key, property.PropertyName, storages[i]));
                }
            }
            return options;
        }

        public static string? GetAssignedKey()
        {
            var key = LegionCore.Api.Save.GetString(PrefKey, string.Empty);
            return string.IsNullOrEmpty(key) ? null : key;
        }

        public static void SetAssignedKey(string key) => LegionCore.Api.Save.SetString(PrefKey, key);

        public static StorageEntity? GetAssignedLocker()
        {
            var key = GetAssignedKey();
            if (key == null) return null;
            return GetEligibleLockers().FirstOrDefault(o => o.Key == key).Storage;
        }
    }
}
