# Delivery Driver ("Fry") — Schedule I Logistics Mod

**Company name:** Global Real Quick Delivery (GRQD)
**Driver:** Fry (Futurama resemblance)
**Type:** Schedule I content mod (MelonLoader / C#, current game version 0.4.6f8+)
**Status:** Spec — build this FIRST (de-risks NPC/vehicle/storage questions before Clean Slate depends on them)
**Author:** Legion

---

## One-line pitch

A repeatable product-delivery van service (Global Real Quick Delivery) that moves your owned product between properties on a schedule, paid daily from a designated locker. Works standalone with vanilla Schedule I and integrates optionally with Clean Slate.

---

## Design Principle

**Own thin middleware layer only.** We build directly against `Assembly-CSharp` for exactly what our mods need — NPC spawn, schedule/pathing, product movement, locker access. No S1API. No other mod dependencies. Third-party mod dependencies are fragile, chain you to someone else's maintenance cadence, and break on version drift (see: OTC). Our layer exposes and interfaces with only what we need — it does not pretend to be the entire game. That narrow surface is dramatically more maintainable. When the game updates, we fix our small owned layer instead of waiting on anyone.

---

## Foundation Decision: Own Layer Only (Version B eliminated)

**No bake-off. We build on our own thin middleware layer against `Assembly-CSharp` directly.**

The bake-off (own layer vs. S1API) was cut after reviewing OTC's actual crash chain. The crashes traced into `PatchS1APIBugs` — IL patches OTC had to write because the *currently maintained* S1API fork ships buggy custom-NPC code. Those patches then crashed inside Mono.Cecil. That is not a stale-fork problem — that is the current, maintained S1API having the same fundamental bug for the exact capability Fry needs (custom NPC creation and scheduling). A bake-off would re-discover what the crash logs already documented. Version B is eliminated.

Build on our own layer. Own the version-compat story. When the game updates, we fix our small surface — not theirs.

---

## Spike Results (all three feasibility questions: GREEN)

Confirmed via Mono.Cecil static inspection of `Assembly-CSharp.dll`. Not yet tested in-game — that is M2.

### 1. Custom dock — feasible
`LoadingDock` is a plain `MonoBehaviour`, not a `GridItem`. Not tied to the tile/placement system. We register our own `MonoBehaviour` via `ClassInjector.RegisterTypeInIl2Cpp` (standard Il2CppInterop) and attach to a `GameObject` at any `Transform`. No dependency on vanilla's per-property dock array.

### 2. Storage-to-storage transfer — feasible, no physical handler needed
`ItemSlot.InsertItem` / `TryInsertItemIntoSet(List<ItemSlot>, ItemInstance)` gives a direct API. A locker-A to locker-B move: pull `ItemInstance` off a slot in A (`ClearStoredInstance`), insert into B via `TryInsertItemIntoSet`. `StorageEntity.ItemSlots : List<ItemSlot>` gives direct slot access. **Constraint:** `StorageEntity` extends `NetworkBehaviour` — transfer must run server-side. Same constraint vanilla lives under, nothing mod-specific.

### 3. Van spawn + navigation — feasible, real road AI included for free
`VehicleManager.SpawnAndReturnVehicle(string vehicleCode, Vector3 position, Quaternion rotation, bool playerOwned)` spawns any vehicle at any world position. `VehicleAgent.Navigate(Vector3 location, NavigationSettings settings, callback)` routes via the game's real road AI (road graph, obstacle sweeps) — same system vanilla traffic and vanilla deliveries use. Real road driving costs nothing extra; we just call the API. Van-only v1 (no driver NPC). `NPC.SetTransform` + `EnterVehicle` exists for a future Fry-visible pass but is explicitly deferred.

---

## Aesthetic / Branding

- **Company:** Global Real Quick Delivery
- **Abbreviation:** GRQD
- **Driver:** Fry (Futurama resemblance — orange/red hair, white shirt + red jacket vibe)
- **Van:** Planet Express green (dark forest), rough hand-tagged GRQD logo on the sides
- **Phone app page:** Planet Express teal, branded as Global Real Quick Delivery
- **IP note:** Futurama / Planet Express is Fox/Disney IP. Character resemblance + rough logo = homage/parody (acceptable). Pixel-perfect logo copy = not acceptable. Keep the logo loose and hand-tagged, not a clean rip.
- **Fry communications:** Fry texts the player via in-game messages using a Fry-lookalike avatar. No physical NPC spawn in v1.

---

## Core Mechanics (v1.0 — the public release)

### Routes
- Player assigns up to 5 routes
- A route = source property/locker to destination property/storage
- Driver cycles through assigned routes on repeat
- Van spawns at the source dock, product is loaded, van navigates to destination via real road AI, product is delivered

### The custom dock
- Our own dock `MonoBehaviour`, spawned at a fixed street-side position per property type (v1 hardcoded defaults):
  - Manor: outside the gate
  - Bungalow: street-side
  - Storage unit: street-side
- Player does not place it in v1 — fixed positions only, kinks ironed out on known locations first
- The dock is a staging point: product is debited from the source locker into the dock staging area, the van loads from there

### Product movement
- Source locker to dock staging: `StorageTransfer` helper (server-side `TryInsertItemIntoSet`)
- Dock to van: van loads from the dock on arrival
- Van to destination storage: `StorageTransfer` on arrival at destination
- Moves owned product only — does not create product

### Payment
- Driver paid daily flat wage (configurable, sits between chemist and street-dealer rates)
- Pay pulled from a player-designated locker at any owned property
- Empty pay locker behavior: TBD (warn player, driver stops — decide in build)

### Delivery loop (one route cycle)
1. Route timer fires
2. Check source locker has product, debit via StorageTransfer to dock staging
3. Spawn GRQD van at the custom dock
4. Van navigates to destination via `VehicleAgent.Navigate` (real road AI)
5. Credit destination storage via StorageTransfer on arrival
6. Deduct daily wage from pay locker
7. Van despawns
8. Cycle to next route

---

## Standalone + Integration

- **Works with vanilla Schedule I** — general-purpose logistics for any multi-property operation
- **Clean-Slate-aware** — when Clean Slate is installed, GRQD can supply the storefront's on-site storage. Neither mod requires the other. Optional integration, no hard dependency either direction.

---

## v1 Scope Line

**In:** GRQD van, up to 5 repeatable routes, product movement between locations, custom fixed-position docks per property type, daily pay from designated locker, Fry texts via avatar, GRQD branding + Planet Express teal app page, vanilla-compatible, Clean-Slate-aware.

**Explicitly deferred:** visible Fry NPC in van, player-placed dock positioning, load-capacity limits per trip, multiple drivers, in-van handler NPC walking to grab product.

---

## Middleware Layer

Three files, each independently testable before integration:

**`Middleware/Docks.cs`** — custom dock component via `ClassInjector.RegisterTypeInIl2Cpp`. Fixed world positions per property type. Staging area for product pending van pickup.

**`Middleware/StorageTransfer.cs`** — locker-to-locker item move helper wrapping `ItemSlot` / `TryInsertItemIntoSet`. Generic: takes source `List<ItemSlot>` and dest `List<ItemSlot>`, moves N units of a given item type. Server-side. Reused by Clean Slate (dock to shelf).

**`Middleware/DeliveryVehicles.cs`** — spawn + navigate wrapper over `VehicleManager.SpawnAndReturnVehicle` + `VehicleAgent.Navigate`. Van-only v1. Real road AI free via `VehicleAgent`. Future hook point for `NPC.SetTransform` + `EnterVehicle` when Fry-visible is added.

---

## Open Questions (decide in build, not before)

1. Can a van get robbed mid-route, or is it inherently safe?
2. Empty pay locker behavior — warn and stop, or warn and continue?
3. Load capacity per trip — unlimited v1, or limit from the start?
4. Exact fixed dock world positions per property type (needs in-game measurement on Vortex)

---

## Build Order

1. `Middleware/Docks.cs` — custom dock at fixed test position
2. `Middleware/StorageTransfer.cs` — locker-to-locker transfer helper
3. `Middleware/DeliveryVehicles.cs` — spawn + navigate wrapper
4. Integration — wire all three into one end-to-end delivery loop, test in-game on Vortex against 0.4.6f8
5. Route UX — GRQD phone app page (teal, up to 5 routes, pay locker designation, route status)
6. Ship — Thunderstore, IP-safe logo pass, photosensitivity note if applicable
