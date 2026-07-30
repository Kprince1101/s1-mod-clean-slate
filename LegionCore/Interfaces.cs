using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.Modification;
using UnityEngine;

namespace LegionCore
{
    public interface IVehicleApi
    {
        bool IsReady { get; }
        LandVehicle? SpawnVan(Vector3 position, Quaternion rotation, EVehicleColor color, bool playerOwned = false);
    }

    public interface IDeliveryApi
    {
        bool IsReady { get; }

        // Registers a tile in the vanilla Delivery app's shop list. Runs the next time
        // DeliveryApp.Start() fires (or immediately if it already has). shopInterfaceName
        // with no matching ShopInterface is fine for now - the shop opens with no listings
        // and ordering is blocked, until a real order-flow ticket wires one up. description
        // and icon are optional (icon falls back to whatever the cloned template tile had).
        void RegisterShopTile(string shopName, Color tileColor, string shopInterfaceName,
            string description = "", Sprite? icon = null);
    }

    public interface INpcApi
    {
        bool IsReady { get; }
    }

    public interface IConfigurableApi
    {
        bool IsReady { get; }
    }

    public interface ISaveApi
    {
        bool GetBool(string key, bool defaultValue = false);
        void SetBool(string key, bool value);
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);
        float GetFloat(string key, float defaultValue = 0f);
        void SetFloat(string key, float value);
        string GetString(string key, string defaultValue = "");
        void SetString(string key, string value);
    }

    public interface INotificationsApi
    {
        bool IsReady { get; }
        void Send(string title, string subtitle, float duration = 5f, bool playSound = true);
    }
}
