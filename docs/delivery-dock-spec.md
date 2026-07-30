# Delivery / Dock Mechanic

Part of the storefront concept (see `PLAN.md`): dealers need product delivered to their
zone, businesses need product delivered to sell. This breaks into three sub-problems:

1. **The dock** — our own staging point, separate from the vanilla one.
2. **Product movement** — storage to dock.
3. **Pickup** — the van arriving and loading.

We didn't know if any of this was feasible before looking at the actual game code, so this
was spiked first. Findings below are from static inspection (Mono.Cecil) of the installed
`Assembly-CSharp.dll` interop stubs — not yet tested in-game.

## Spike questions and findings

### 1. Can we place a custom dock/staging object at an arbitrary location?

Vanilla `Il2CppScheduleOne.Delivery.LoadingDock` is a plain `MonoBehaviour` (not a
`GridItem`/`BuildableItem`), and it's design-time-baked: each `Property` holds a fixed
`LoadingDock[] LoadingDocks` array set in the scene, not something spawned at runtime through
any vanilla API.

That's fine — we don't need to reuse `LoadingDock` itself, and its non-grid nature is good
news: it means a dock isn't tied to the tile/grid placement system that constrains regular
furniture (`GridItem`, tile-footprint-based). We can register our own `MonoBehaviour` type
into the Il2Cpp domain (`ClassInjector.RegisterTypeInIl2Cpp`, standard Il2CppInterop
technique) and attach it to a `GameObject` at any `Transform` — no dependency on vanilla's
per-property array.

**Verdict: feasible.** Build our own dock component, not a reuse of `LoadingDock`.

### 2. Can we move product between two storage locations programmatically?

Yes, cleanly. `Il2CppScheduleOne.ItemFramework.ItemSlot` (what both `StorageEntity` and
`LoadingDock` use for their slot lists) exposes:

- `InsertItem(ItemInstance)`, `AddItem(ItemInstance, bool)`, `ClearStoredInstance(bool)`
- `SetQuantity` / `ChangeQuantity`
- `TryInsertItemIntoSet(List<ItemSlot>, ItemInstance)` — handles finding a slot in a target
  set that accepts the item.

`StorageEntity.ItemSlots : List<ItemSlot>` gives direct access to a locker's contents. A
locker-A -> locker-B move is: pull the `ItemInstance` off a slot in A, insert into B via
`TryInsertItemIntoSet`. `StorageEntity` extends `NetworkBehaviour`, so this needs to run
server-side (or through a networked call) — same constraint vanilla code lives under, nothing
mod-specific.

**Verdict: feasible**, no physical handler needed.

### 3. Where does the vanilla delivery driver spawn, and can we spawn at a custom point?

`Il2CppScheduleOne.Vehicles.VehicleManager` (a `NetworkSingleton`) exposes:

- `SpawnAndReturnVehicle(string vehicleCode, Vector3 position, Quaternion rotation, bool
  playerOwned)` — spawns any vehicle at any world position, not restricted to a fixed spawn
  point.
- `Vehicles.AI.VehicleAgent.Navigate(Vector3 location, NavigationSettings settings,
  callback)` — sends a spawned vehicle driving to an arbitrary destination via the game's
  real AI pathing (road graph, obstacle sweeps, etc. — the same system regular traffic and
  vanilla deliveries use).

Vanilla's own `DeliveryVehicle`/`DeliveryManager` looks like a pooled set of pre-placed
vehicles that get `Activate(DeliveryInstance)`'d rather than spawned fresh, but the general
`VehicleManager` spawn API is not limited to that pool.

For a driver NPC specifically: `NPCs.NPC.SetTransform(conn, position, rotation)` allows
direct placement, and `EnterVehicle(conn, LandVehicle)` puts an NPC in a vehicle. Not
required for a first version — a driverless/auto-piloted van (as several vanilla delivery
vehicles already effectively are) is a valid v1 scope cut.

**Verdict: feasible for the vehicle.** GRQD is van-only — no driver NPC, no `NPC.SetTransform`/
`EnterVehicle` usage; the van moves on its own via `VehicleAgent.Navigate`.

## What this unblocks

All three original unknowns resolve to "buildable," not "rabbit hole." None of the three
requires deep engine-level work or reverse-engineering beyond what's already been done here.

## Still open (needs a decision, not a spike)

- Where a modded dock visually/physically sits relative to a zone (needs actual placement
  design, not just API feasibility).
- Whether business income/rent (see `PLAN.md`) hooks into `Business.MinsPass`/`TimeSkipped`
  (both already exist on the vanilla `Business` class) or needs its own timer in the
  middleware interface.

## Next steps

Build order once picked up as real work (each should be its own ticket):

1. Middleware: `Middleware/Docks.cs` — custom dock component via `ClassInjector`, placeable
   at a fixed test point.
2. Middleware: `Middleware/StorageTransfer.cs` — locker-to-locker item move helper wrapping
   `ItemSlot`/`TryInsertItemIntoSet`.
3. Middleware: `Middleware/DeliveryVehicles.cs` — spawn + navigate wrapper over
   `VehicleManager`/`VehicleAgent`.
4. Integration: wire dock -> storage transfer -> van pickup into one end-to-end delivery flow,
   tested in-game.
