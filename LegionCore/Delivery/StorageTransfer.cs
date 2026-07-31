using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Storage;
using UnityEngine;

namespace LegionCore.Delivery
{
    // Generic locker-to-locker (or locker-to-van, van-to-locker) item mover. Wraps the real
    // vanilla API confirmed via the M1 spike (grqd-spec.md §2): ItemSlot.TryInsertItemIntoSet
    // for the insert side, StorageEntity.HowManyCanFit for capacity, ItemInstance.GetCopy for
    // splitting a stack. No custom item/inventory model - just moves the player's own
    // StorableItemInstances between two StorageEntity.ItemSlots lists.
    //
    // Server-side only: StorageEntity is a NetworkBehaviour and every mutation this triggers
    // (SetStoredInstance/SetItemSlotQuantity, reached via ItemSlot.SetStoredItem/
    // ChangeQuantity/ClearStoredInstance) routes through a [ServerRpc(RunLocally = true)] -
    // same constraint vanilla itself lives under, nothing mod-specific. Single player is
    // always host, so this works as-is; a dedicated-server build would need an explicit host
    // guard here first.
    public static class StorageTransfer
    {
        // Moves as many whole/partial stacks as fit, up to maxUnits total, from source into
        // dest. No per-item-type filtering yet - moves whatever is sitting in source, same
        // "moves your owned product" framing as the spec (Route doesn't carry a specific item
        // selection today - see RouteModels.cs). Returns total units actually moved.
        public static int MoveAll(StorageEntity? source, StorageEntity? dest, int maxUnits = int.MaxValue)
        {
            if (source == null || dest == null || maxUnits <= 0) return 0;

            int moved = 0;
            // Index-based on purpose - Il2Cpp-backed List<ItemSlot>, and we're mutating slots
            // as we go (same interop-safe pattern DockRegistry/LockerRegistry already use
            // instead of foreach).
            for (int i = 0; i < source.ItemSlots.Count && moved < maxUnits; i++)
            {
                var slot = source.ItemSlots[i];
                var item = slot.ItemInstance;
                if (item == null || slot.Quantity <= 0) continue;

                int want = Mathf.Min(slot.Quantity, maxUnits - moved);
                int canFit = dest.HowManyCanFit(item);
                int transfer = Mathf.Min(want, canFit);
                if (transfer <= 0) continue;

                if (transfer >= slot.Quantity)
                {
                    // Whole stack - detach the live instance from source and hand the same
                    // object to dest (TryInsertItemIntoSet stacks it onto a matching slot or
                    // drops it into an empty one). ClearStoredInstance only clears the SLOT's
                    // reference to the instance, it doesn't mutate the instance object itself,
                    // so reusing `item` afterward is safe.
                    slot.ClearStoredInstance();
                    ItemSlot.TryInsertItemIntoSet(dest.ItemSlots, item);
                }
                else
                {
                    // Partial stack - shrink source in place, hand dest a copy sized to
                    // exactly what's moving.
                    slot.ChangeQuantity(-transfer);
                    ItemSlot.TryInsertItemIntoSet(dest.ItemSlots, item.GetCopy(transfer));
                }
                moved += transfer;
            }
            return moved;
        }
    }
}
