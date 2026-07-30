using System;
using UnityEngine;

namespace LegionCore.Delivery
{
    // GRQD's own pickup-side staging point - separate from vanilla LoadingDock, not tied to
    // the tile/grid placement system (see docs/delivery-dock-spec.md). Registered into the
    // Il2Cpp domain via ClassInjector.RegisterTypeInIl2Cpp<PickupDock>() (GRQD/Plugin.cs).
    // Fixed arbitrary position per property, player doesn't place it in v1 (per grqd-spec.md).
    //
    // Pickup-only: this is where the van loads product debited from the property's assigned
    // locker. It is never a delivery destination - destinations are vanilla LoadingDocks
    // (or, later, the Clean Slate storefront).
    public class PickupDock : MonoBehaviour
    {
        // Il2Cpp interop requires this constructor on any injected type - objects are
        // constructed from a native pointer, not the parameterless managed ctor.
        public PickupDock(IntPtr ptr) : base(ptr) { }

        public string PropertyCode = string.Empty;
    }
}
