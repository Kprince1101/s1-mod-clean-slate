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
- Player defines up to 5 routes. Definition pattern intentionally mirrors the vanilla
  Handler system's existing route setup, so GRQD's UX feels native instead of reinventing
  one — **not yet spiked**: a static keyword search of `Assembly-CSharp.dll` for `Handler`
  found no logistics/dead-drop system by that name (only UI/dialogue/effect handlers). The
  vanilla system this refers to exists under some other class name — needs an in-game or
  broader static look before Route UX gets built, don't guess the data model from the name
  alone.
- Each route is defined independently, in two separate steps:
  1. **Schedule** — daily, weekly, etc. Cost scales with cadence (exact cost table TBD).
  2. **Start and finish**, set separately:
     - **Start** — a storage unit (property), optionally a specific locker/shelf within it.
     - **Finish** — without Clean Slate: any owned property's vanilla `LoadingDock` that
       isn't full. With Clean Slate installed: the storefront is also a valid finish.
- Each route runs on its **own** configured schedule — not one shared cycle across all 5. A
  daily route fires every day regardless of what a weekly route is doing.

### Pickup docks — source-side only, not a general dock
- Our own dock `MonoBehaviour`, spawned at a fixed street-side position per property type
  (v1 hardcoded defaults): Manor outside the gate, Bungalow/storage unit street-side. Player
  doesn't place it in v1.
- **This dock is pickup-only.** It exists solely as a staging point at a route's *start*: the
  van loads from it after product is debited from the source locker/shelf into it. It is not
  a delivery destination and has no function beyond that one job.
- **Destinations are vanilla docks, not ours.** A route's *finish* targets an existing
  `Il2CppScheduleOne.Delivery.LoadingDock` already on the destination property (or the
  storefront, with Clean Slate) — not a second instance of our pickup dock.

### Destination capacity check
- "Not full" is checked with the vanilla API already identified in the M1 spike:
  `DeliveryManager.IsLoadingBayFree(Property destination, int loadingDockIndex)`. No need to
  write our own capacity logic for vanilla-dock destinations. Clean Slate storefront capacity
  check is separate, TBD in that mod.

### Product movement
- Source locker/shelf to pickup-dock staging: `StorageTransfer` (server-side
  `TryInsertItemIntoSet`)
- Pickup dock to van: van loads from the dock on arrival
- Van to destination `LoadingDock`/storefront storage: `StorageTransfer` on arrival
- Moves owned product only — does not create product

### Payment
- No separately-designated pay locker. Pay lives at the route's **first stop** (the pickup) —
  Fry collects his wage from a locker there as part of that stop, cost scaled to the route's
  schedule (see Routes above).
- **Open question, not yet resolved:** does "first stop" mean *each route's own* pickup has
  its own pay locker, or is it *one* locker at whichever route happens to run first on a
  given day (when multiple routes' schedules overlap)? Materially different data model
  (per-route pay locker vs. one global pay locker) — needs a decision before build, not a
  guess.
- Empty pay locker behavior: TBD (warn player, driver stops — decide in build)

### Delivery loop (one route's run)
1. That route's configured schedule fires (independent per route)
2. Check source locker/shelf has product, debit via `StorageTransfer` to the pickup dock
3. Spawn GRQD van at the pickup dock; collect wage from the locker there (see open question
   above on scope)
4. Van navigates to the destination `LoadingDock`/storefront via `VehicleAgent.Navigate`
5. Check destination not full (`IsLoadingBayFree` for vanilla dock destinations; storefront
   check TBD)
6. Credit destination storage via `StorageTransfer`
7. Van despawns

---

## Standalone + Integration

- **GRQD is fully standalone.** Works with vanilla Schedule I, no dependency on Clean Slate,
  general-purpose logistics for any multi-property operation.
- **The dependency runs one direction.** Clean Slate doesn't hard-require GRQD installed to
  function, but its core loop assumes it: without automated delivery the player is stuck
  manually restocking constantly, which defeats running an actual storefront operation.
  That's the real reason GRQD is built first — not just de-risking shared capabilities, but
  because Clean Slate isn't practically playable without it. "Highly suggested," not
  hard-enforced.

---

## v1 Scope Line

**In:** GRQD van, up to 5 independently-scheduled routes, product movement between a source locker/shelf and a vanilla `LoadingDock` (or storefront, with Clean Slate), pickup-only custom staging dock, pay collected at each route's first stop, Fry texts via avatar, GRQD branding + Planet Express teal app page, vanilla-compatible, Clean-Slate-aware.

**Explicitly deferred:** visible Fry NPC in van, player-placed dock positioning, load-capacity limits per trip, multiple drivers, in-van handler NPC walking to grab product.

---

## Middleware Layer

Three files, each independently testable before integration:

**`Middleware/Docks.cs`** — custom dock component via `ClassInjector.RegisterTypeInIl2Cpp`. Fixed world positions per property type. Pickup-side staging only, for product pending van pickup — not used as a delivery destination (destinations are vanilla `LoadingDock`s or the storefront).

**`Middleware/StorageTransfer.cs`** — locker-to-locker item move helper wrapping `ItemSlot` / `TryInsertItemIntoSet`. Generic: takes source `List<ItemSlot>` and dest `List<ItemSlot>`, moves N units of a given item type. Server-side. Reused by Clean Slate (dock to shelf).

**`Middleware/DeliveryVehicles.cs`** — spawn + navigate wrapper over `VehicleManager.SpawnAndReturnVehicle` + `VehicleAgent.Navigate`. Van-only v1. Real road AI free via `VehicleAgent`. Future hook point for `NPC.SetTransform` + `EnterVehicle` when Fry-visible is added.

---

## Open Questions (decide in build, not before)

1. Can a van get robbed mid-route, or is it inherently safe?
2. Pay-locker scope — per-route pay locker, or one shared locker at whichever route runs
   first that day? (See Payment section above — not yet decided.)
3. Empty pay locker behavior — warn and stop, or warn and continue?
4. Load capacity per trip — unlimited v1, or limit from the start?
5. What the vanilla "Handler" route system this is meant to mirror actually is — a keyword
   scan for `Handler` in `Assembly-CSharp.dll` found no logistics/dead-drop match, only
   UI/dialogue/effect classes. Needs a real look (in-game or a broader static search) before
   Route UX gets designed.
6. Exact fixed dock world positions per property type (needs in-game measurement on Vortex)

---

## Build Order

1. `Middleware/Docks.cs` — custom dock at fixed test position
2. `Middleware/StorageTransfer.cs` — locker-to-locker transfer helper
3. `Middleware/DeliveryVehicles.cs` — spawn + navigate wrapper
4. Integration — wire all three into one end-to-end delivery loop, test in-game on Vortex against 0.4.6f8
5. Route UX — GRQD phone app page (teal, up to 5 routes, pay locker designation, route status)
6. Ship — Thunderstore, IP-safe logo pass, photosensitivity note if applicable
