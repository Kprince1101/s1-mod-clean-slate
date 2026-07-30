# Agent report: 1_NPC_Employee

Now I have thorough coverage. Here is the structured report.

## 1. NPC Spawning/Instantiation

**Two distinct patterns coexist — critical distinction for the middleware:**

**A) Named/scripted NPCs (dealers, customers, townsfolk, Dealer subclass instances) are NOT runtime-spawned.** They are scene-baked `NetworkObject`s that self-register on `Awake()`. Evidence in `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.NPCs/NPC.cs`:
- Each `NPC` has a `BakedGUID` field (line 158) set at scene-bake time.
- `Awake_UserLogic_..._NPC_...()` (line 3112): if `GUID == Guid.Empty`, it validates `BakedGUID` via `GUIDManager.IsGUIDValid` and self-registers: `NPCManager.NPCRegistry.Add(this)` (line 3120-3123).
- `Start()` (line 464) reparents the NPC under `NetworkSingleton<NPCManager>.Instance.NPCContainer`.
- `NPCManager.cs` (`/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.NPCs/NPCManager.cs`) has **zero** `Instantiate`/`Spawn` calls — it's purely a registry (`NPCRegistry`), a static lookup (`GetNPC(string id)`, `GetNPCsInRegion`), and a `ISaveable` implementation. This confirms Clean Slate/GRQD must work against *existing* pre-placed dealer NPCs, never spawn one — consistent with the stated crash history and rule against custom NPC creation.

**B) Employees (Botanist/Chemist/Cleaner/Packager) ARE runtime FishNet-spawned**, via `EmployeeManager.CreateEmployee_Server` (`/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Employees/EmployeeManager.cs:90-123`):
```csharp
Employee component = UnityEngine.Object.Instantiate(employeePrefab, position, rotation).GetComponent<Employee>();
component.Initialize(null, firstName, lastName, id, guid, property.PropertyCode, male, appearanceIndex);
base.NetworkObject.Spawn(component.gameObject);
```
This is `Object.Instantiate` + `NetworkObject.Spawn` (FishNet), server-RPC gated (`[ServerRpc(RequireOwnership = false)]` on `CreateEmployee`), and only reachable from server logic. This spawn path is irrelevant to Clean Slate's dealer redirection (dealers use path A), but relevant background if the middleware ever needs employee-style spawn semantics.

## 2. Dealer NPC subclass and its street-deal decision flow

`Dealer : NPC, IItemSlotOwner` in `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Economy/Dealer.cs` (2509 lines). Key decision chain, driven every tick:

- `Dealer.OnTick()` (line 271-287) → if server and not loading: `CheckAttendStart()` if no `currentContract`, else `CheckCurrentDealValidity()`.
- `CheckAttendStart()` (line 425-448): pulls `ActiveContracts.FirstOrDefault()`, computes a travel-time window based on `Vector3.Distance(Avatar.CenterPoint, contract.DeliveryLocation.CustomerStandPoint.position)`, and if within window: sets `currentContract = contract`, then `_attendDealBehaviour.Enable_Server(); _attendDealBehaviour.AssignContract(currentContract);`.
- The actual street-deal execution lives in `DealerAttendDealBehaviour` (`/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.NPCs.Behaviour/DealerAttendDealBehaviour.cs`), a `Behaviour` subclass:
  - `Activate()`/`Resume()` call `SetDestination(GetStandPosition())`, where `GetStandPosition()` (line 151-158) returns `_contract.DeliveryLocation.CustomerStandPoint.position + ...forward * 1.2f` — **this is the exact street-deal location, driven entirely by the `DeliveryLocation.CustomerStandPoint` transform.**
  - `OnActiveTick()` (line 65-101): when at destination and `IsCustomerReadyForHandover()` (delegates to `_customer.IsAtDealLocation()`), calls `BeginHandover()`.
  - `BeginHandover()` (line 103-130): coroutine → faces customer, waits for `_customer.IsAtDealLocation()`, plays `GrabItem` anim, then server-side: `_dealer.RemoveContractItems(...)`, `_customer.OfferDealItems(items, offeredByPlayer:false, out accepted)`, then `Disable_Server()`.

**`Customer.IsAtDealLocation()`** (`/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Economy/Customer.cs:1217-1236`) is the gate on the customer side:
```csharp
return Vector3.Distance(base.transform.position, CurrentContract.DeliveryLocation.CustomerStandPoint.position) < 1f;
```

**Redirect hook point — `DeliveryLocation`:** `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Economy/DeliveryLocation.cs` is a plain `MonoBehaviour, IGUIDRegisterable` with just `CustomerStandPoint`/`TeleportPoint` transforms and a `StaticGUID` string. Its `Awake()` only self-registers if the scene-baked `StaticGUID` validates via `GUIDManager.IsGUIDValid`; if invalid it just logs an error and leaves `GUID == Guid.Empty` — it does **not** throw or disable the object. Since both dealer-side (`DealerAttendDealBehaviour.GetStandPosition()`) and customer-side (`Customer.IsAtDealLocation()`) logic reference `contract.DeliveryLocation.CustomerStandPoint` and nothing else, **the entire street-deal geometry is fully determined by one `DeliveryLocation` component reference on the `Contract`/`ContractInfo`.** This is the cleanest non-destructive redirect: a mod can instantiate a `DeliveryLocation` component on a GameObject placed at the storefront counter, manually call `((IGUIDRegisterable)location).SetGUID(Guid.NewGuid())` (bypassing the static-baked-GUID check, mirroring how `NPC.SetGUID`/`Employee.Initialize` register arbitrary runtime GUIDs elsewhere — see `NPC.cs:422-426`), and then have that instance's `CustomerStandPoint` point at the counter. No vanilla dealer/customer/behaviour code needs to change — they already only read `DeliveryLocation.CustomerStandPoint`.

Also note: `ContractInfo` (`/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Quests/ContractInfo.cs`) resolves `DeliveryLocation` lazily via `GUIDManager.GetObject<DeliveryLocation>(new Guid(DeliveryLocationGUID))` if the cached reference is null (lines 44-56), so a GUID-string swap on an existing/new `Contract` also works if direct-reference swap is unavailable in Il2Cpp interop.

Dealer's customer roster (separate from the per-deal handoff) is tracked via `Dealer.AssignedCustomers` / `Dealer.AddCustomer(Customer)` (line 498-505) and `RemoveCustomer` — pure C# list ops, safe to read/enumerate from a mod.

## 3. Employee base class — task/schedule assignment pattern

`Employee : NPC` (`/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Employees/Employee.cs`) provides the generic scaffold all four employee types build on:

- `AssignProperty(Property prop, bool warp)` (line 212-222): `AssignedProperty = prop; EmployeeIndex = AssignedProperty.RegisterEmployee(this); ... WaitOutside.IdlePoint = prop.EmployeeIdlePoints[EmployeeIndex];`
- `OnTick()` (line 479-485) → `UpdateBehaviour()` every tick, which checks `CanWork()`/`GetHome()`/pay status, and if no active behaviour or the idle one is active, either forces `SetWaitOutside(true)` (enables `WaitOutside : IdleBehaviour`) or pays the employee.
- Subclasses (e.g. `Botanist`) override `UpdateBehaviour()` and add a `_workBehaviours` list (`/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Employees/Botanist.cs:101,176-184`), each entry a `Behaviour` subclass with a `.AssignAndEnable(target)` pattern, e.g. `_waterPotBehaviour.AssignAndEnable(potForWatering); ... _harvestPotBehaviour.AssignAndEnable(potsForHarvest[0]);`. `IsAnyWorkInProgress()` is overridden to check `_workBehaviours.Any(b => b.Active)`.

**Reusable pattern for "dealer working a storefront zone":** this is exactly the `IdleBehaviour` used for `Employee.WaitOutside` (`/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.NPCs.Behaviour/IdleBehaviour.cs`) — a generic `Behaviour` with a public `Transform IdlePoint` field; `OnActiveTick()` walks to `IdlePoint.position` and faces `IdlePoint.forward` once arrived, purely via `SetDestination`/`Movement.FaceDirection`. It requires no target-specific setup beyond assigning the `IdlePoint` transform. If the Dealer prefab has (or can be given) a spare `IdleBehaviour`/`StationaryBehaviour` child component, pointing its `IdlePoint` at the storefront counter and calling `Enable_Server()` would hold the dealer at the counter using stock, already-networked behaviour code — no new `NetworkBehaviour` types needed. (Caveat: Dealer's default `NPCBehaviour.behaviourStack` is populated from `GetComponentsInChildren<Behaviour>()` at edit-time `OnValidate()`, not at runtime — see §5.)

## 4. Non-destructive hook points into the NPC behaviour/schedule state machine

Two independent systems layer on top of `NPC`, both viable hook surfaces without touching NPC internals:

**A) `NPCBehaviour` (priority-stack state machine)** — `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.NPCs.Behaviour/NPCBehaviour.cs`:
- Holds a `List<Behaviour> behaviourStack`, sorted by `Priority` (`SortBehaviourStack()`, line 271-278).
- `Update()` (line 209-245, host-only path): picks `GetEnabledBehaviour()` = `enabledBehaviours.FirstOrDefault()` (already sorted by priority) and transitions `activeBehaviour` via `Pause_Server()`/`Activate_Server()`/`Resume_Server()`.
- **Public, safe entry points for a mod**: `NPCBehaviour.GetBehaviour<T>()` / `GetBehaviour(string name)` (line 194-207) to fetch an existing `Behaviour` component already on the dealer (e.g. cast to `DealerAttendDealBehaviour`, `IdleBehaviour`, `GenericDialogueBehaviour`), then drive it via the existing public API: `Enable_Server()`, `Disable_Server()`, `Activate_Server()`, `Deactivate_Server()`, all of which are `[ServerRpc(RequireOwnership=false, RunLocally=true)]` — safe to call from any authority context as long as it's called server-side. Priority-based preemption means a mod can add a *higher-priority* existing behaviour (if a spare one is present on the prefab) to take over from the vanilla `DealerAttendDealBehaviour` without disabling it directly.

**B) `NPCScheduleManager` (time-of-day action list)** — `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.NPCs/NPCScheduleManager.cs`. This drives `NPCAction`s (`ScheduleOne.NPCs.Schedules`) by time-of-day (`ActionList`, `GetActionsOccurringAt`, `StartAction`). This is for daily routines (walk-to-work-at-X-time), separate from Dealer's per-deal logic. Less relevant to Clean Slate's real-time counter redirection, but relevant to GRQD if a van driver NPC has a scheduled dispatch window — the pattern (`NPCAction.ShouldStart()`, `.Started()`, `.Interrupt()`, `.JumpTo()`) is the equivalent hook set.

**Recommended non-destructive approach for Clean Slate**, combining findings from §2–4: don't touch `Dealer`/`DealerAttendDealBehaviour` classes at all. Instead (a) redirect `DeliveryLocation.CustomerStandPoint` per-contract to the storefront counter (§2), letting all vanilla per-deal logic run unmodified at the new location; and/or (b) drive the existing `DealerAttendDealBehaviour`/`IdleBehaviour` via their public `Enable_Server()`/`AssignContract()`/`IdlePoint` API to make the dealer idle at the counter between deals.

## 5. Gotchas

- **Il2Cpp/FishNet dual `Awake()` split.** Every `NetworkBehaviour` (including `NPC`, `Dealer`, `Employee`, `Behaviour`, `NPCBehaviour`) has generated `NetworkInitialize___Early()` / user-logic `Awake_UserLogic_...()` / `NetworkInitialize__Late()` triple-call pattern inside `Awake()` (e.g. `NPC.cs:362-367`). SyncVars, RPC registration, and `beh = GetComponentInParent<NPCBehaviour>()` (Behaviour.cs:318) all happen inside these — a mod component added via Harmony/MelonLoader patches must not run before this completes, or `Npc`/`beh` properties on `Behaviour`/`NPCBehaviour` will NPE.
- **`NPCBehaviour.behaviourStack` is populated only in the Unity editor.** `OnValidate()` (`NPCBehaviour.cs:116-121`) does `behaviourStack = GetComponentsInChildren<Behaviour>().ToList(); SortBehaviourStack();` — this runs at edit time, not at runtime `Awake`. At runtime the `behaviourStack` is whatever was serialized into the prefab/scene. **Adding a new `Behaviour` child GameObject to a dealer at runtime via a mod will NOT auto-register it in `behaviourStack`**, meaning `Enable_Server()`/priority arbitration in `NPCBehaviour.Update()` will never activate it. Any custom behaviour would have to be manually inserted into `behaviourStack`/`enabledBehaviours` (private/serialized fields — needs reflection under Il2Cpp) or, better, reuse an existing stack member as recommended in §4.
- **Server authority everywhere.** Nearly all state-changing dealer/behaviour methods are `[ServerRpc(RequireOwnership = false, RunLocally = true)]` (e.g. `Behaviour.Enable_Server`, `Dealer.AddCustomer_Server`, `Dealer.SetCash`, `Dealer.CompletedDeal`). `Behaviour.SetDestination` (line 246-260) and `Dealer.CheckAttendStart`/`OnTick` gate all movement/logic on `InstanceFinder.IsServer`. In multiplayer, the middleware must run its redirect logic host/server-side only, mirroring vanilla's own `InstanceFinder.IsServer` checks, or state will silently no-op on clients.
- **`GUIDManager.cs` was not present in this decompiled extraction** (searched the full tree — only `IGUIDRegisterable.cs` interface exists here). Its exact validation rules (`IsGUIDValid`, `IsGUIDAlreadyRegistered`, `RegisterObject`, `GetObject<T>`, `GenerateUniqueGUID`) are inferred from call sites only (`Dealer.cs`, `NPC.cs:422-426`, `DeliveryLocation.cs`, `Employee.cs:747`, `ContractInfo.cs`). Before relying on runtime `SetGUID()` for a synthetic `DeliveryLocation` (§2), verify against the actual `GUIDManager` implementation (likely in a core/persistence assembly not included in this dump) that manual registration of a non-baked GUID doesn't get rejected or collide with save/load GUID reconciliation.
- **`DealerAttendDealBehaviour` nulls its state in `Deactivate()`** (`_contract = null; _customer = null;`) and `CheckCurrentDealValidity()` (`Dealer.cs:395-403`) force-disables `_attendDealBehaviour` and clears `currentContract` the moment `contract.State != EQuestState.Active`. Any mod-side redirection logic that holds its own reference to the contract/behaviour must not assume it persists past a single tick — re-fetch from `Dealer.ActiveContracts`/`currentContract` (private — needs a public accessor or reflection) each time.
- **`overflowSlots` and dialogue choice objects (`recruitChoice`, `assignCustomersChoice`, etc.) are private fields on `Dealer`** with no public accessor — inventory manipulation must go through `Dealer.AddItemToInventory`/`GetAllSlots()`/`RemoveContractItems` (public) rather than touching slots directly.
- **`Health.onDieOrKnockedOut` triggers `Dealer.DealerUnconscious()`** which force-fails all `ActiveContracts` (`Dealer.cs:342-349`). If Clean Slate's storefront flow keeps a dealer "busy" via a custom idle/attend state, that state must still respect the health/knockout system (`NPCBehaviour.OnKnockOut()` disables all behaviours except `Dead`/`Unconscious`) or it'll silently coexist incorrectly with a downed dealer.

**Key files referenced** (all under `/tmp/s1-mod-clean-slate/reference/decompiled/`):
- `ScheduleOne.NPCs/NPC.cs`, `NPCManager.cs`, `NPCScheduleManager.cs`
- `ScheduleOne.NPCs.Behaviour/Behaviour.cs`, `NPCBehaviour.cs`, `DealerAttendDealBehaviour.cs`, `IdleBehaviour.cs`, `StationaryBehaviour.cs`
- `ScheduleOne.Economy/Dealer.cs`, `Customer.cs`, `DeliveryLocation.cs`
- `ScheduleOne.Employees/Employee.cs`, `EmployeeManager.cs`, `Botanist.cs`
- `ScheduleOne.Quests/ContractInfo.cs`, `Contract.cs`
- `ScheduleOne.Map/NPCPoI.cs`
- `ScheduleOne/IGUIDRegisterable.cs`agentId: ad1e8935a0c360f8e (use SendMessage with to: 'ad1e8935a0c360f8e', summary: '<5-10 word recap>' to continue this agent)
<usage>subagent_tokens: 175409
tool_uses: 33
duration_ms: 210884</usage>
---

# Agent report: 2_Vehicle_Nav

## 1. `SpawnAndReturnVehicle` — full real body and FishNet spawn behavior

Confirmed real body, `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Vehicles/VehicleManager.cs:73-92`:

```csharp
public LandVehicle SpawnAndReturnVehicle(string vehicleCode, Vector3 position, Quaternion rotation, bool playerOwned)
{
    LandVehicle vehiclePrefab = GetVehiclePrefab(vehicleCode);
    if (vehiclePrefab == null)
    {
        Console.LogError("SpawnVehicle: '" + vehicleCode + "' is not a valid vehicle code!");
        return null;
    }
    LandVehicle component = UnityEngine.Object.Instantiate(vehiclePrefab.gameObject).GetComponent<LandVehicle>();
    component.transform.position = position;
    component.transform.rotation = rotation;
    Physics.SyncTransforms();
    base.NetworkObject.Spawn(component.gameObject);
    component.SetIsPlayerOwned(null, playerOwned);
    if (playerOwned)
    {
        PlayerOwnedVehicles.Add(component);
    }
    return component;
}
```

**It DOES do the FishNet network spawn internally** — `base.NetworkObject.Spawn(component.gameObject)`. `base.NetworkObject` here is `VehicleManager`'s own (already-spawned) `NetworkObject` component; `.Spawn(GameObject)` is a FishNet instance-method wrapper that ultimately routes to `ServerManager.Spawn`. This exact call pattern (`base.NetworkObject.Spawn(...)`) is used identically elsewhere in the codebase — `BuildManager.cs:191/207/223`, `EmployeeManager.cs:114`, `NetworkedEquipper.cs:55/89`, `MushroomBed.cs:388` — confirming it's the game's standard idiom for server-side dynamic spawning, not something unique to vehicles.

**Caller obligation / gotcha**: `SpawnAndReturnVehicle` has **no server-authority guard of its own**. The public entry point that *does* guard it is the paired `[ServerRpc(RequireOwnership=false)] SpawnVehicle(...)`, whose reader checks `if (base.IsServerInitialized) RpcLogic___SpawnVehicle_3323115898(...)` before calling `SpawnAndReturnVehicle`. If a mod calls `SpawnAndReturnVehicle` directly (bypassing the RPC), it is responsible for guaranteeing it's running with server authority (host or dedicated server) itself — `NetworkObject.Spawn` will fail/log a warning if called from a non-authoritative context. In practice, for a MelonLoader singleplayer session the local client is host, so `InstanceFinder.IsHost` / `NetworkSingleton<VehicleManager>.Instance.IsServerInitialized` will be true and direct calls are safe.

Also note `Physics.SyncTransforms()` is called *before* `Spawn` — needed because colliders/physics state must be settled before the object is network-spawned (bounding box checks etc. run in `Awake`/`Start` on spawn).

`GetVehiclePrefab` (line 94-97) does a case-insensitive lookup by `ToLower()` against `VehicleManager.VehiclePrefabs` (an Inspector-populated `List<LandVehicle>`), returning `null` on miss and only then logging the error and returning `null` — no exception thrown, so a bad `vehicleCode` fails silently unless the caller checks for `null`.

## 2. Color persistence mechanism — `ApplyColor` is purely cosmetic

**Critical finding: `LandVehicle.ApplyColor(EVehicleColor)` (line 1365-1368) is purely cosmetic and must NOT be used for persistence:**

```csharp
public void ApplyColor(EVehicleColor col)
{
    Color.ApplyColor(col);   // Color = VehicleColor component field
}
```

It just forwards to `VehicleColor.ApplyColor` (`ScheduleOne.Vehicles/VehicleColor.cs:35-53`), which swaps material colors on `BodyMeshes` renderers — no state is recorded anywhere. `OwnedColor` (the property actually saved/synced) is untouched.

**`OwnedColor` is NOT a FishNet `[SyncVar]`** — it's a plain C# auto-property (`LandVehicle.cs:376`) manually kept in sync via a hand-rolled RPC pair that mimics SyncVar semantics:

- `SendOwnedColor(EVehicleColor col)` — `[ServerRpc(RequireOwnership=false, RunLocally=true)]` (line 1343-1348) — the entry point any client (or the host locally) calls to request a color change. `RpcLogic___SendOwnedColor_911055161` (line 2184-2187) just does `SetOwnedColor(null, col)`.
- `SetOwnedColor(NetworkConnection conn, EVehicleColor col)` — `[TargetRpc][ObserversRpc(RunLocally=true)]` (line 1350-1363) — dual-mode dispatcher: `conn == null` → broadcast to all observers; `conn != null` → target a single reconnecting/newly-joined client. Its `RpcLogic___SetOwnedColor_1679996372` (line 2226-2231) is the actual mutation:
```csharp
protected virtual void RpcLogic___SetOwnedColor_1679996372(NetworkConnection conn, EVehicleColor col)
{
    OwnedColor = col;
    ApplyOwnedColor();   // -> Color.ApplyColor(OwnedColor)
    RefreshPoI();
}
```

So the real chain is: **`SendOwnedColor` → server sets `OwnedColor` + calls `ApplyOwnedColor()` (visual) + broadcasts `SetOwnedColor` ObserversRpc → every client (including late joiners via `OnSpawnServer`) sets its own `OwnedColor` and reapplies the visual.**

**Late-join sync**: `OnSpawnServer(NetworkConnection connection)` (line 485-508) explicitly calls `SetOwnedColor(connection, OwnedColor)` as a `TargetRpc` for any newly-connecting (non-host) client, so a vehicle spawned/colored before a second player joins still displays correctly to them.

**Persistence**: `GetVehicleData()` (line 1485-1488) reads `OwnedColor` directly: `new VehicleData(GUID, vehicleCode, transform.position, transform.rotation, OwnedColor, ...)`. `VehicleData.Color` is `col.ToString()` (an enum name string). On load, `Load(VehicleData data, ...)` (line 1515-1519) calls `SetOwnedColor(null, Enum.Parse<EVehicleColor>(data.Color))` — i.e., load also goes through the same RPC-dispatch path (with `conn=null`, so it's the local/server-authoritative path, not actually re-broadcast over network since load happens pre-spawn-to-clients in this flow).

**Default color reset gotcha**: `Awake_UserLogic_..._LandVehicle` (line ~2508) unconditionally does `OwnedColor = Color.DefaultColor;` (where `Color` is the `VehicleColor` component, whose `DefaultColor` field is `EVehicleColor.White` unless overridden per-prefab in the Inspector) — **every freshly-instantiated vehicle resets `OwnedColor` to the prefab's default on `Awake`, before `Start`, before any save data is applied.** `SpawnAndReturnVehicle` takes no color parameter at all.

**What the mod must actually do to make a freshly-spawned van's color persist:**
1. Call `SpawnAndReturnVehicle(...)` (which internally resets `OwnedColor` to prefab default in `Awake`).
2. After spawn, call `landVehicle.SendOwnedColor(desiredColor)` (server/host-callable, `RequireOwnership=false`) — this sets `OwnedColor`, applies the visual, and (if other clients are connected) broadcasts it.
3. Persistence works automatically from that point because `GetVehicleData()`/save reads `OwnedColor`, which is now correctly set.

Directly calling `ApplyColor` or setting `OwnedColor` via reflection without going through `SendOwnedColor`/`SetOwnedColor` will either not persist (if you skip the property) or persist an un-replicated value that other clients never see (if you set the property directly without the RPC broadcast) — visually correct only for the host until the object round-trips through `OnSpawnServer` for another connection.

## 3. `VehicleAgent.Navigate` — signature, settings, failure behavior

`ScheduleOne.Vehicles.AI/VehicleAgent.cs:708-761`:

```csharp
public void Navigate(Vector3 location, NavigationSettings settings = null, NavigationCallback callback = null)
```
where `public delegate void NavigationCallback(ENavigationResult status)` and `enum ENavigationResult { Failed, Complete, Stopped }`.

`NavigationSettings` (`ScheduleOne.Vehicles.AI/NavigationSettings.cs`) is a plain 3-field POCO, all with defaults, so `null` is legal (a default instance is constructed internally: `if (settings == null) settings = new NavigationSettings();`):
```csharp
public class NavigationSettings
{
    public bool endAtRoad;
    public bool ensureProximityToGraph = true;
    public bool teleportToGraphIfCalculationFails = true;
}
```

**Host-only gate, silent no-op**: right after handling stuck/reverse cleanup, `Navigate` does:
```csharp
if (!InstanceFinder.IsHost) { return; }
```
On a non-host client, `Navigate` returns immediately with **no callback invocation at all** — not even `Failed`. Only a host (or dedicated server, since `InstanceFinder.IsHost` is true there too under FishNet's host semantics, though pure dedicated-server should be checked via `IsServerInitialized`/`IsServer` more precisely — the game code uses `IsHost` specifically) actually drives pathfinding. This is a strong argument that Delivery Driver's spawn+navigate flow must run host-side.

**Failure paths**:
- If the vehicle is further than 6m from the "General Vehicle Graph" (`GetDistanceFromVehicleGraph() > 6f`):
  - if `settings.ensureProximityToGraph` (default `true`) → `Teleporter.MoveToGraph()` is called to snap it onto the graph, and navigation proceeds.
  - else, if a `callback` was supplied → `callback(ENavigationResult.Failed)` and `return` (no path calculated).
- Actual pathfinding is async via `NavigationUtility.CalculatePath(...)` (a coroutine returning a callback `NavigationCalculationCallback`). If `NavigationUtility.ENavigationCalculationResult.Failed` is returned (e.g., unreachable/no path found by the A* graph), `NavigationCalculationCallback` invokes `storedNavigationCallback(ENavigationResult.Failed)` and calls `EndDriving()` (resets `AutoDriving=false`, sensors, steer/throttle overrides, clears `path`).
- On success, `Complete` fires from `UpdateSpeedReduction()` when the vehicle gets within 3m (`DestinationArrivalThreshold`) of the path's final point — that's the actual "arrived" signal, not a return from `Navigate` itself.
- `StopNavigating()` externally cancels and fires `ENavigationResult.Stopped`.

`Navigate` also forcibly sets `vehicle.overrideControls = true` and `vehicle.OverrideMaxSteerAngle(35f)` — i.e., calling `Navigate` hands full drive control to the AI (throttle/steer are computed in `LateUpdate`/`InfrequentUpdate`), so a mod must not also be setting `overrideControls`/`throttleOverride` externally at the same time.

`DriveFlags` (`ScheduleOne.Vehicles.AI/DriveFlags.cs`) is a separate, richer per-vehicle behavior config (`vehicleAgent.Flags`) controlling `OverrideSpeed`/`OverriddenSpeed`, `SpeedLimitMultiplier`, `IgnoreTrafficLights`, `UseRoads`, `StuckDetection`, `ObstacleMode`, `AutoBrakeAtDestination`, `TurnBasedSpeedReduction` — this is distinct from `NavigationSettings` and is read live every `InfrequentUpdate`/`UpdateSpeedReduction` tick, so it can be tuned after `Navigate` is already called (e.g., raise `OverriddenSpeed`/set `OverrideSpeed=true` for a faster delivery van, or `IgnoreTrafficLights=true`).

## 4. Vehicle code registry — no compile-time source exists

There is **no static/compile-time registry of vehicle codes anywhere in the decompiled C#.** Every occurrence of a vehicle-code string as *data* rather than plumbing is either:
- A hardcoded literal inside game logic for one specific case: `VehicleManager.SpawnLoanSharkVehicle` uses `"shitbox"` directly (`VehicleManager.cs:131`).
- Unity Inspector-populated data with **no C# literal at all** — `VehicleManager.VehiclePrefabs` (`List<LandVehicle>`) is populated purely via the scene/prefab asset, and each `LandVehicle.vehicleCode` (`[SerializeField] protected string vehicleCode = "vehicle_code";`, line 66) is a serialized field set per-prefab in the Unity editor, not in code. `"veeper"` (the van) will never appear as a string literal in any decompiled `.cs` file — it only exists baked into the prefab's serialized data.
- The dealership listing system (`Jeremy.DealershipListing.vehicleCode`, `ScheduleOne.NPCs.CharacterClasses/Jeremy.cs:15-22`) is likewise a `[Serializable]` class whose `vehicleCode` values are populated per-NPC-instance in the Inspector (`Jeremy.Listings`), resolved at runtime via `NetworkSingleton<VehicleManager>.Instance.GetVehiclePrefab(vehicleCode)`.

**Practical consequence**: a modder cannot validate vehicle codes from source/compile-time. The only reliable *runtime* validation path is exactly what `GetVehiclePrefab` does — enumerate `VehicleManager.Instance.VehiclePrefabs` (or `AllVehicles`) at game startup and read `.VehicleCode` off each prefab, then cache that list in the middleware for fast/typed lookup (e.g., build an `enum`/const list once at `OnApplicationStart`/first scene load and validate against it before calling `SpawnAndReturnVehicle`). There's no ScriptableObject database, no JSON/config manifest, and no static class listing all vehicle codes in the reference source — confirming the prior Cecil-stub finding that `"veeper"` is only knowable by having actually inspected the live prefab list (or dealership listings) at runtime, not from any source artifact.

## 5. FishNet gotchas for MelonLoader-driven spawns

- **Spawn requires server/host authority.** `VehicleManager.SpawnAndReturnVehicle` calls `base.NetworkObject.Spawn(component.gameObject)` with no internal guard — the guard lives one layer up, in the `ServerRpc`'s reader (`if (base.IsServerInitialized) { ... }`, `VehicleManager.cs:210`). A MelonLoader mod calling `SpawnAndReturnVehicle` (or the lower-level `NetworkObject.Spawn`) directly must first confirm the local instance has server authority — in singleplayer that's the host (`InstanceFinder.IsHost` true), consistent with the identical unguarded pattern used by `BuildManager`, `EmployeeManager`, and `NetworkedEquipper` for their own dynamic spawns. Calling it from a plain client-only context is unsupported by this codebase's own conventions.

- **Ownership is NOT automatically the local player.** `LandVehicle.OnStartServer()` (line 473-483) does `base.NetworkObject.GiveOwnership(base.LocalConnection)` — i.e., on spawn, FishNet ownership goes to the **host's local connection**, not to any specific player, and is independent of the game-level `IsPlayerOwned` flag (which is a separate, non-FishNet, "is this vehicle assigned to a player's garage" concept set via `SetIsPlayerOwned`). Actual driver-based ownership transfer only happens when a player physically enters the driver's seat: `EnterVehicle()` (line 1197-1220) calls `base.NetworkObject.SetLocalOwnership(Player.Local.Connection)` client-side and `SetOwner(Player.Local.Connection)` (a `ServerRpc`) to make it authoritative server-side. A delivery-van mod that never seats a player in the driver's seat (fully AI-driven) will simply keep host ownership indefinitely — which is fine, since `VehicleAgent.Navigate` explicitly requires `InstanceFinder.IsHost` anyway, so host-owned + host-driven is the natural/only supported configuration for autonomous delivery vans.

- **`RequireComponent` dependencies matter for manual instantiation.** `LandVehicle` is decorated with `[RequireComponent(typeof(NetworkTransform))] [RequireComponent(typeof(PredictedOwner))] [RequireComponent(typeof(PhysicsDamageable))]` (lines 40-42) — if the middleware ever needs to build a vehicle from scratch (rather than cloning an existing prefab via `SpawnAndReturnVehicle`, which is the safe path since it clones a fully-configured prefab), all three FishNet/game components must be present or `NetworkObject` registration/prediction will misbehave.

- **`SendOwnedColor`/color RPCs use `RequireOwnership = false`.** Both `SendOwnedColor` and the seat/steering RPCs are marked `RequireOwnership = false`, meaning the middleware does not need to hold FishNet ownership of the van to change its color or query/set most state — but it does still need to be running server-side for the `ServerRpc` to execute its logic (`RpcReader___Server_SendOwnedColor_911055161` checks `base.IsServerInitialized`, `LandVehicle.cs:2189-2196`, with `RunLocally=true` meaning host callers execute `RpcLogic` synchronously without a network round-trip).

- **`base.NetworkObject` in `VehicleManager.SpawnAndReturnVehicle` is the *manager's* NetworkObject, not the vehicle's.** This is a slightly non-obvious FishNet idiom worth flagging for the middleware author: the `.Spawn(GameObject)` call is invoked on an *already-spawned* NetworkObject purely to reach the spawn API surface — any already-spawned `NetworkBehaviour`'s `.NetworkObject.Spawn(newGameObject)` works identically (same pattern reused verbatim in `BuildManager`, `EmployeeManager`, `NetworkedEquipper`, `MushroomBed`). The middleware doesn't need `VehicleManager` specifically to spawn network objects generically — but for vehicles specifically it should go through `VehicleManager.SpawnAndReturnVehicle` anyway since that's what also registers the vehicle into `PlayerOwnedVehicles`/`AllVehicles` and wires the prefab lookup.

- **No FishNet source was available to verify internals of `NetworkObject.Spawn`** (only `FishNet.Runtime.dll` at `/tmp/s1-mod-clean-slate/reference/managed-mono/FishNet.Runtime.dll`, not decompiled) — all conclusions above about its requiring server authority are inferred from consistent call-site usage across the ScheduleOne codebase (unconditional call from methods only reachable via `IsServerInitialized`-gated RPCs), not from FishNet's own source.agentId: a97301ed48415552d (use SendMessage with to: 'a97301ed48415552d', summary: '<5-10 word recap>' to continue this agent)
<usage>subagent_tokens: 109908
tool_uses: 24
duration_ms: 176664</usage>
---

# Agent report: 3_Delivery_Dock_Storage

Now I have the full picture. Here is the report.

## 1. Vanilla delivery call chain — what actually moves items, and hookable events

**There is no vanilla code path that moves items from a `LoadingDock` into a destination `Property`'s storage.** The dock never holds inventory itself. The full chain, code-cited:

1. **Order placed** — `DeliveryShop.SubmitOrder` (`ScheduleOne.UI.Phone.Delivery/DeliveryShop.cs:123`) builds a `List<StringIntPair>` cart, constructs a `DeliveryInstance(guid, MatchingShopInterfaceName, destinationProperty.PropertyCode, loadingDockIndex - 1, items, EDeliveryStatus.InTransit, deliveryTime)`, deducts money via `MoneyManager.CreateOnlineTransaction`, and calls `NetworkSingleton<DeliveryManager>.Instance.SendDelivery(deliveryInstance)` (a `[ServerRpc(RequireOwnership=false)]`) plus `RecordDeliveryReceipt_Server`.
2. **Server accepts** — `DeliveryManager.RpcLogic___SendDelivery_2813439055` → `ReceiveDelivery(null, delivery)` → `RpcLogic___ReceiveDelivery_2795369214` (`DeliveryManager.cs:438`): `Deliveries.Add(delivery); delivery.SetStatus(delivery.Status);` then fires **`public event Action<DeliveryInstance> onDeliveryCreated`**. This is a genuine hook point.
3. **Ticking** — every game minute, `TimeManager.onMinutePass`/`onTimeSkip` drives `DeliveryManager.OnTimePass(int minutes)` (`DeliveryManager.cs:114`), server-only (`InstanceFinder.IsServer`). For each `DeliveryInstance`: `deliveryInstance.OnTimePass(minutes)` decrements `TimeUntilArrival`. When it hits 0 and status isn't `Arrived`:
   - `IsLoadingBayFree(destination, loadingDockIndex)` → `!destination.LoadingDocks[loadingDockIndex].IsInUse` (`DeliveryManager.cs:163`).
   - If free: `deliveryInstance.AddItemsToDeliveryVehicle()` then `SetDeliveryState(id, Arrived)`.
   - If busy: `SetDeliveryState(id, Waiting)` — order literally queues in real time waiting for the dock.
4. **Items materialize in the van, not the dock** — `DeliveryInstance.AddItemsToDeliveryVehicle()` (`DeliveryInstance.cs:92`) resolves `PlayerSingleton<DeliveryApp>.Instance.GetShop(StoreName).MatchingShop.DeliveryVehicle`, then for each `StringIntPair`, chunks quantity by `item.StackLimit` and calls `deliveryVehicle.Vehicle.Storage.InsertItem(defaultInstance)` — i.e. `StorageEntity.InsertItem` on the **van's own `StorageEntity`** (`ScheduleOne.Storage/StorageEntity.cs:183`).
5. **Vehicle parks at the dock** — `SetDeliveryState(Arrived)` → `DeliveryInstance.SetStatus` (`DeliveryInstance.cs:70`): `ActiveVehicle = ...DeliveryVehicle; ActiveVehicle.Activate(this)`. `DeliveryVehicle.Activate` (`DeliveryVehicle.cs:25`) parks the `LandVehicle` at `instance.LoadingDock.Parking`, calls `instance.LoadingDock.SetStaticOccupant(Vehicle)`, sets `Vehicle.Storage.AccessSettings = EAccessSettings.Full`, opens the storage door animation. A notification is sent to the player (`DeliveryManager.cs:535`).
6. **Player empties it manually** — nothing in vanilla code moves items out of the van into the property's storage. `LoadingDock.InputSlots` is *never populated anywhere* (grep confirms zero writers besides the field initializer) and `LoadingDock.OutputSlots` is only ever `.AddRange(DynamicOccupant.Storage.ItemSlots)` — i.e. it's a **live alias into whichever vehicle is physically sitting on the dock's trigger** (`LoadingDock.SetOccupant`, `LoadingDock.cs:116`), refreshed once/second via `InvokeRepeating("RefreshOccupant", ..., 1f)`. The dock is a pass-through view onto the parked vehicle's `StorageEntity.ItemSlots`, nothing more. The player opens the van's `StorageEntity` (`StorageEntity.Open()`) like any other storage container and drags items into inventory/property storage by hand.
7. **Completion** — while `Status == Arrived`, `OnTimePass` tracks `_minsSinceVehicleEmpty`; once the van's `Storage.ItemCount == 0` and `CurrentPlayerAccessor == null` for 3 consecutive in-game minutes, `SetDeliveryState(Completed)` fires **`public event Action<DeliveryInstance> onDeliveryCompleted`**, calls `ActiveVehicle.Deactivate()` (drives off, clears `StaticOccupant`/`VehicleDetector`), and removes the entry from `Deliveries`.

**Hooks a mod can subscribe to instead of reimplementing:**
- `DeliveryManager.onDeliveryCreated` (`Action<DeliveryInstance>`) — order placed/received.
- `DeliveryManager.onDeliveryCompleted` (`Action<DeliveryInstance>`) — delivery fully emptied & vehicle departing.
- `StorageEntity.onContentsChanged` / `onOpened` / `onClosed` (`StorageEntity.cs:58-62`) — per-container change events, fired from `ItemSlot.onItemDataChanged`.
- There is **no** `onItemDelivered`/`onDockChanged` event at the dock level — the dock exposes no events at all (see §4).

For a "van moves items from source storage → dock → destination storage" mod, the vanilla model gives you nothing to hook for step 3 (dock→destination); that leg is 100% custom code in vanilla too (it's literally manual drag-and-drop by the player). Your middleware's cleanest anchor points are `StorageEntity.InsertItem`/`ItemSlot.TryInsertItemIntoSet` for the actual transfer, and optionally the `DeliveryManager` events/`DeliveryInstance`/`LoadingDock.IsInUse` machinery if you want dock reservation/queuing semantics identical to vanilla's.

## 2. `ItemSlot` / `TryInsertItemIntoSet` — signature and failure handling

`ItemSlot.TryInsertItemIntoSet` (`ScheduleOne.ItemFramework/ItemSlot.cs:403`):
```csharp
public static bool TryInsertItemIntoSet(List<ItemSlot> ItemSlots, ItemInstance item)
```
Behavior: first pass tops off any slot whose `ItemInstance.CanStackWith(item)` is true (skipping `IsLocked`/`IsAddLocked` slots) up to `item.StackLimit - existing.Quantity`; second pass places any remainder into the first empty, unlocked slot via `SetStoredItem`. **Returns `true` only if the entire quantity was absorbed** (`num <= 0`); if slots run out mid-item, it returns `false` **but any partial amount already inserted stays inserted** — there is no rollback/transaction. Callers must check the bool and handle the remainder themselves.

Failure modes and how vanilla actually handles them (mirror these in middleware):
- **Wrong item type**: `ItemInstance.CanStackWith` (`ItemInstance.cs:37`) checks `other.ID != base.ID` → false, slot skipped. `ItemSlot.AddItem` (`ItemSlot.cs:151`) separately guards this with `Console.LogWarning("AddItem called with item that cannot stack with current item. Refusing.")` — it does **not** throw, just logs and no-ops.
- **Slot full / stack limit**: `GetCapacityForItem` (`ItemSlot.cs:381`) returns `item.StackLimit - Quantity` (or 0 if hard filters/player filters reject it); `CanStackWith(other, checkQuantities:true)` also refuses if `Quantity + other.Quantity > StackLimit`.
- **Locked slot**: `IsLocked` (has an `ActiveLock`) or `IsAddLocked` slots are always skipped in both passes; `SetStoredItem`/`ChangeQuantity` additionally hard-refuse (`Console.LogError(...); return;`) if called directly on a locked slot outside `_internal` bypass.
- **Removal-locked**: `ClearStoredInstance`/negative `ChangeQuantity` on an `IsRemovalLocked` slot is refused with a log, no exception.
- **Hard filters / cash**: `DoesItemMatchHardFilters` checks `HardFilters` list and specially requires `CanSlotAcceptCash()` for `CashInstance` items.
- **No exceptions anywhere** — the entire `ItemFramework`/`Storage` transfer surface is exception-free; every failure is a `Console.LogWarning`/`LogError` plus a silent no-op or partial application. A middleware wrapper should replicate this "log + partial-apply + bool/remainder return" contract rather than throwing, and should **always check remaining quantity after calling `TryInsertItemIntoSet`/`InsertItemIntoInput`/`StorageEntity.InsertItem`**, since none of them guarantee full insertion.

Related overload worth using instead of hand-rolling a loop: `StorageEntity.CanItemFit(item, quantity)` / `HowManyCanFit(item)` (`StorageEntity.cs:158-181`) — pre-check capacity before calling `InsertItem`, since `InsertItem` itself just logs a warning and **returns doing nothing** if `!CanItemFit(item, item.Quantity)` (`StorageEntity.cs:183-189`), i.e. it's all-or-nothing at the top level even though the underlying slot loop is capable of partial fill.

## 3. `Property.LoadingDocks` structure and addressing

```csharp
// Property.cs
public LoadingDock[] LoadingDocks;
public int LoadingDockCount => LoadingDocks.Length;
```
Plain fixed-size array set in the Unity inspector per property prefab — no dictionary, no name keys. **Addressing is purely by integer index into this array.** Confirmed by every consumer:
- `DeliveryInstance.LoadingDockIndex` (int) + `DeliveryInstance.LoadingDock => Destination.LoadingDocks[LoadingDockIndex]` (`DeliveryInstance.cs:40`).
- `DeliveryManager.IsLoadingBayFree(Property destination, int loadingDockIndex)` → `destination.LoadingDocks[loadingDockIndex].IsInUse`.
- `DeliveryShop` UI just populates a dropdown `1..LoadingDockCount` and stores `loadingDockIndex - 1` (`DeliveryShop.cs:146,363-386`).
- `LoadingDock.Name => "Loading Dock " + (ParentProperty.LoadingDocks.IndexOf(this) + 1)` — the display "name" is *derived from* the index, not an independent identifier; there's no dock-level `Name`/code field to look up by.

Property-level lookup **is** name/code-based, and that part of the prior research holds: `PropertyManager.GetProperty(string code)` (`PropertyManager.cs:120`) does `Property.UnownedProperties.FirstOrDefault(p => p.PropertyCode == code) ?? Property.OwnedProperties.FirstOrDefault(p => p.PropertyCode == code)`. `PropertyCode`/`PropertyName` are serialized inspector fields on `Property` (`propertyCode`/`propertyName`, `Property.cs:52-56,128-130`). So the official/clean pattern is: **`Property` by `PropertyCode` via `PropertyManager.GetProperty(code)`, then `LoadingDock` by plain `int` index into `property.LoadingDocks[i]`.** There is no cleaner alternative in the source — no GUID-keyed dock registry is used for delivery addressing (though `LoadingDock` does implement `IGUIDRegisterable`/has its own `GUID` for the general `GUIDManager` object-registry system used elsewhere, e.g. potential `TransitRoute` save/load via GUID as seen in `MoveItemBehaviour.Load`, but delivery code itself never uses the dock's GUID — it always uses the raw array index, both in `DeliveryInstance`/`DeliveryReceipt` (which serialize `int LoadingDockIndex`) and in save data).

Caveat: index-based addressing means **dock identity is not stable if `Property.LoadingDocks` is ever reordered/rebuilt** — fine for vanilla static prefabs, but a middleware wrapping this should not assume index stability across mod-injected dock arrays without also preserving order.

## 4. Existing dock/storage event system to subscribe to

- **Dock level: none.** `LoadingDock` has zero `Action`/`UnityEvent` fields — nothing fires when occupancy changes, when a vehicle parks, or when `IsAcceptingItems`/`IsDestroyed` change. `SetOccupant`/`SetStaticOccupant` are plain field setters with only a `Console.Log` line, no event.
- **Delivery level:** `DeliveryManager.onDeliveryCreated` and `onDeliveryCompleted` (both `Action<DeliveryInstance>`, `DeliveryManager.cs:65-67`) are the only delivery-system-wide events. `DeliveryInstance` also exposes a per-instance `[NonSerialized] public UnityEvent onDeliveryCompleted` (`DeliveryInstance.cs:31`), invoked in `SetStatus` when status becomes `Completed` — usable for a single order you're tracking.
- **Storage level:** `StorageEntity.onOpened`, `onClosed`, `onContentsChanged` (`StorageEntity.cs:58-62`) — these fire on any slot mutation via `ItemSlot.onItemDataChanged` wiring done in `StorageEntity.Awake_UserLogic...` (`StorageEntity.cs:1066-1070`). This is the best available hook for "something changed in this storage container," but it's per-`StorageEntity` instance, not global, and you must subscribe per-container.
- **Slot level:** `ItemSlot.onItemDataChanged`, `onItemInstanceChanged`, `onLocked`, `onUnlocked`, `onFilterChange` — fine-grained, per-slot.

Conclusion: you cannot avoid building your own dock component/event surface for GRQD's van↔dock↔storage flow — vanilla's `LoadingDock` is intentionally dumb (a parking-trigger proxy), and the only "official" transfer/event machinery lives at `StorageEntity`/`ItemSlot`. Building a custom dock component that composes `StorageEntity.onContentsChanged` + your own occupancy events is consistent with vanilla's own architecture, not a deviation from it.

## 5. Gotchas

- **Server authority is non-negotiable and asymmetric per call.** `StorageEntity.SetStoredInstance`/`SetItemSlotQuantity`/`SetSlotLocked`/`SetSlotFilter` are all `[ServerRpc(RunLocally = true, RequireOwnership = false)]` (`StorageEntity.cs:307-399`). `ItemSlot.SetStoredItem`/`ChangeQuantity`/`SetQuantity` route through these via `SlotOwner.SetStoredInstance(...)` whenever `SlotOwner != null && !_internal` (`ItemSlot.cs:107-257`). Calling these from client code without an active server connection just logs `"Cannot complete action because client is not active..."` and does nothing (`StorageEntity.cs:436-452` pattern repeated for every RPC). Your middleware must gate all storage-mutating calls to run server-side (`InstanceFinder.IsServer`/host), exactly like `DeliveryManager.OnTimePass` does (`if (!InstanceFinder.IsServer) continue;`, `DeliveryManager.cs:124`).
- **`StorageEntity.InsertItem(item, network=true)` will silently no-op** if `!CanItemFit(item, item.Quantity)` — no partial fill at this level (`StorageEntity.cs:183-189`), unlike `ItemSlot.TryInsertItemIntoSet` which partial-fills. If your source→dest transfer needs partial-fill semantics (e.g. van has more product than destination storage can hold), you must call the lower-level `ItemSlot.TryInsertItemIntoSet(destSlots, item)` yourself and handle the leftover, not `StorageEntity.InsertItem`.
- **`_internal:true` bypasses networking entirely.** Passing `network:false`/`_internal:true` mutates the local `ItemSlots` list without any RPC, meaning other clients in multiplayer never see the change — desync. Only use this path if you separately broadcast the change yourself (matches how `RpcLogic___SetStoredInstance_Internal_*` reader methods use `_internal:true` *after* already being routed through the RPC system).
- **Save-data duplication risk.** `DeliveryManager.GetSaveString()` snapshots `delivery.ActiveVehicle.Vehicle.GetVehicleData()` for every active `DeliveryInstance` into `Deliveries/Deliveries.json` (`DeliveryManager.cs:221-232`). If your mod's van reuses the vanilla `DeliveryVehicle`/`DeliveryInstance` types to get this persistence "for free," you inherit its exact save/load semantics (arrival timers, dock index, item list as `StringIntPair[]`, `EDeliveryStatus`) — including the 3-minute idle-and-empty completion heuristic. If instead you build a fully custom van/dock, you must write your own `ISaveable`/`IBaseSaveable` persistence for van position, contents, and in-flight state, or a save/reload will lose or corrupt in-transit deliveries. `StorageEntity` itself is not independently `ISaveable` — whatever owns it (Property/Business/vehicle) is responsible for serializing its `ItemSlots` (see `StorageEntity.LoadFromItemSet`), so a custom dock-side storage needs the same wiring.
- **Dock occupancy is polled, not reserved atomically against outside actors.** `LoadingDock.IsInUse` = `DynamicOccupant != null || StaticOccupant != null`. `DynamicOccupant` is only refreshed once/second via `InvokeRepeating("RefreshOccupant", random 0-1s offset, 1f)` and physical trigger-collider detection (`VehicleDetector`, sqr-distance-gated to 400 (20m) via `OnTick`). `StaticOccupant` is set synchronously by `DeliveryVehicle.Activate`/`LoadingDock.SetStaticOccupant`, so vanilla's *own* concurrent deliveries are race-free only because `DeliveryManager.OnTimePass` processes all `Deliveries` sequentially in one server-side foreach and marks the dock occupied (`SetStaticOccupant`) in the same synchronous call as the `IsLoadingBayFree` check that gated it. **If a mod's van targets the same `LoadingDock` index without going through `DeliveryManager`/`SetStaticOccupant`, there is no shared reservation**: vanilla will consider the dock free (based on stale/absent occupant state) and can dispatch a real delivery into a dock your mod's van is already using, causing two vehicles to try to occupy the same parking spot/`ParkingSpots[0]`. Any custom dock component must either (a) also drive `LoadingDock.SetStaticOccupant`/keep `IsInUse` truthful so vanilla's `IsLoadingBayFree` respects it, or (b) maintain a fully separate reservation table and never let vanilla `DeliveryShop` orders target docks your mod is using (e.g. by making `Property.CanDeliverToProperty()`/dock count reflect only vanilla-safe docks).
- **`EAccessSettings` on the van's `StorageEntity`** is force-set to `Full` on arrival (`DeliveryVehicle.Activate`, `Vehicle.Storage.AccessSettings = EAccessSettings.Full`) and the storage door animation is overridden open — if your mod also opens/closes this storage programmatically, coordinate with `Deactivate()` which forces the vehicle invisible and off-map (`SetTransform(0,-100,0)`) and clears `StaticOccupant`/`VehicleDetector`; don't hold a `StorageEntity` reference to a deactivated delivery vehicle across that transition.
- **Quality/stacking mismatches**: `ItemInstance.CanStackWith` only compares `ID` (and quantity vs `StackLimit`); subclasses like `QualityItemInstance` override this to also require matching quality — a middleware transfer routine that only checks base `ID` will incorrectly consider two different-quality stacks stackable unless it dispatches through the real (overridden) `CanStackWith`.

**Files referenced** (all under `/tmp/s1-mod-clean-slate/reference/decompiled/`):
`ScheduleOne.Delivery/{LoadingDock,DeliveryManager,DeliveryInstance,DeliveryReceipt,DeliveryVehicle,DeliveryConfiguration,EDeliveryStatus}.cs`, `ScheduleOne.ItemFramework/{ItemSlot,ItemInstance,ItemSlotSiblingSet}.cs`, `ScheduleOne.Management/{ITransitEntity,TransitRoute}.cs`, `ScheduleOne.NPCs.Behaviour/MoveItemBehaviour.cs`, `ScheduleOne.Storage/StorageEntity.cs`, `ScheduleOne.Property/{Property,PropertyManager,PropertyContentsContainer}.cs`, `ScheduleOne.UI.Phone.Delivery/DeliveryShop.cs`, `ScheduleOne.DevUtilities/{StringIntPair,VehicleDetector}.cs`, `ScheduleOne.Persistence.Datas/DeliveriesData.cs`, `ScheduleOne/IGUIDRegisterable.cs`.agentId: a92f4be60658dba9d (use SendMessage with to: 'a92f4be60658dba9d', summary: '<5-10 word recap>' to continue this agent)
<usage>subagent_tokens: 124934
tool_uses: 36
duration_ms: 237768</usage>
---

# Agent report: 4_Property_Mgmt_IConfigurable

I have a complete, verified answer now. Here is the structured report.

## 1. Complete list of every `IConfigurable` implementor (exhaustive, grep-verified)

Interface definition: `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.Management/IConfigurable.cs`

Grepping the entire decompiled tree for `class \w+.*IConfigurable` / `: IConfigurable` (and separately confirming **no interface extends `IConfigurable`**, so there is no indirect-implementor path) yields exactly **15 concrete classes**, no more, no fewer:

| # | Class | File |
|---|---|---|
| 1 | `Pot` | `ScheduleOne.ObjectScripts/Pot.cs` (`Pot : GrowContainer, IConfigurable`) |
| 2 | `MushroomBed` | `ScheduleOne.ObjectScripts/MushroomBed.cs` (`: GrowContainer, IConfigurable`) |
| 3 | `ChemistryStation` | `ScheduleOne.ObjectScripts/ChemistryStation.cs` (`: GridItem, IUsable, IItemSlotOwner, ITransitEntity, IConfigurable`) |
| 4 | `LabOven` | `ScheduleOne.ObjectScripts/LabOven.cs` (same pattern as above) |
| 5 | `PlaceableStorageEntity` | `ScheduleOne.ObjectScripts/PlaceableStorageEntity.cs` (`: GridItem, ITransitEntity, IUsable, IConfigurable`) |
| 6 | `Cauldron` | `ScheduleOne.ObjectScripts/Cauldron.cs` |
| 7 | `DryingRack` | `ScheduleOne.ObjectScripts/DryingRack.cs` |
| 8 | `BrickPress` | `ScheduleOne.ObjectScripts/BrickPress.cs` |
| 9 | `PackagingStation` | `ScheduleOne.ObjectScripts/PackagingStation.cs` |
| 10 | `MixingStation` | `ScheduleOne.ObjectScripts/MixingStation.cs` |
| 11 | `MushroomSpawnStation` | `ScheduleOne.StationFramework/MushroomSpawnStation.cs` (`: GridItem, IUsable, IItemSlotOwner, ITransitEntity, IConfigurable`) |
| 12 | `Botanist` | `ScheduleOne.Employees/Botanist.cs` (`: Employee, IConfigurable`) |
| 13 | `Cleaner` | `ScheduleOne.Employees/Cleaner.cs` |
| 14 | `Chemist` | `ScheduleOne.Employees/Chemist.cs` |
| 15 | `Packager` | `ScheduleOne.Employees/Packager.cs` |

Notes on base classes: neither `GrowContainer` (`ScheduleOne.Growing/GrowContainer.cs`, `abstract class GrowContainer : GridItem, IUsable, ITransitEntity`) nor `Employee` (`ScheduleOne.Employees/Employee.cs`, `class Employee : NPC`) implements `IConfigurable` itself — the interface is declared explicitly on each leaf subclass, so `Pot`/`MushroomBed` and `Botanist`/`Cleaner`/`Chemist`/`Packager` each carry their own duplicate `EConfigurableType`/`Configuration`/etc. plumbing rather than inheriting it.

This maps 1:1 onto the 15 values of the closed enum `EConfigurableType` in `ScheduleOne.Management/EConfigurableType.cs` (`Pot, PackagingStation, LabOven, Botanist, Packager, ChemistryStation, Chemist, Cauldron, Cleaner, BrickPress, MixingStation, DryingRack, SpawnStation, MushroomBed, Storage` — `Storage` = `PlaceableStorageEntity`). This is a closed, non-flags enum with no room for new members without a binary patch — see gotcha in §5.

Files that reference `IConfigurable` but do **not** implement it (they consume the interface type): `ManagementClipboard.cs`, `ManagementClipboard_Equippable.cs`, `ManagementInterface.cs`, `ManagementWorldspaceCanvas.cs`, `SelectionInfoUI.cs`, `Property.cs`, `EntityConfiguration.cs`, `ConfigurationReplicator.cs`, and every `*Configuration.cs` class (they hold a back-reference `IConfigurable Configurable` and take one in their constructor).

## 2. What `IConfigurable` requires; minimal implementation for a mod object

`ScheduleOne.Management/IConfigurable.cs` requires:

```csharp
EntityConfiguration Configuration { get; }
ConfigurationReplicator ConfigReplicator { get; }
EConfigurableType ConfigurableType { get; }
WorldspaceUIElement WorldspaceUI { get; set; }
NetworkObject CurrentPlayerConfigurer { get; set; }
bool IsBeingConfiguredByOtherPlayer { get; }      // default impl: !CurrentPlayerConfigurer.IsOwner
Sprite TypeIcon { get; }
Transform Transform { get; }
Transform UIPoint { get; }
bool IsDestroyed { get; }                         // default impl: Transform == null
bool CanBeSelected { get; }
ScheduleOne.Property.Property ParentProperty { get; }
WorldspaceUIElement CreateWorldspaceUI();
void DestroyWorldspaceUI();
void ShowOutline(Color color);
void HideOutline();
void Selected();                                  // default: Configuration.Selected()
void Deselected();                                // default: Configuration.Deselected()
void SetConfigurer(NetworkObject player);
void SendConfigurationToClient(NetworkConnection conn);
```

Minimal shape a mod object needs (modeled directly on `Pot.cs`, the smallest real implementor):
- A `NetworkBehaviour` (must live on a `NetworkObject` — `ConfigurationReplicator` is itself a `NetworkBehaviour` performing FishNet `ServerRpc`/`ObserversRpc` field replication, so the configurable object must be networked; `CurrentPlayerConfigurer` is a FishNet `SyncVar`).
- A companion `ConfigurationReplicator` component (or a Harmony-free custom subclass) referenced by `ConfigReplicator`.
- A subclass of `EntityConfiguration` (e.g. `MyRouteConfiguration : EntityConfiguration`) built in the constructor out of existing reusable field types from `ScheduleOne.Management`: `StringField`, `NumberField`, `ItemField`, `ObjectField`, `ObjectListField`, `NPCField`, `RouteListField`, `QualityField`, `StationRecipeField` (all subclass `ConfigField`, which self-registers into `EntityConfiguration.Fields` on construction — see `ConfigField.cs`). This is precisely the reusable part: **a mod does not need to invent new field-widget UI** — `RouteListField` (routes, `AdvancedTransitRoute`) and `ObjectField`/`ObjectListField` (single/multi target-picker, exactly what a "pay-locker" designation needs) already exist and already have generic UI drivers.
- `WorldspaceUI` populated via `CreateWorldspaceUI()` (instantiate a `WorldspaceUIElement` prefab under `ParentProperty.WorldspaceUIContainer` and call an `Initialize(this)`-style method, as `Pot.CreateWorldspaceUI()` does with `PotUIElement`).
- `ConfigurableType` returning a value from `EConfigurableType` — **this is the hard constraint**: the enum is closed (15 vanilla values), so a new mod type must either reuse an existing value that "fits" (risky — colors typing checks like `SelectionInfoUI`/`ManagementWorldspaceCanvas`'s "uniform type" grouping) or the enum must be extended via IL/Harmony patch, and `ManagementInterface.ConfigPanelPrefabs` (a serialized `ConfigurableTypePanel[]` array on the prefab) must gain a matching entry — see §5.
- `AddConfigurable`/`RemoveConfigurable` calls into the owning `Property` at spawn/destroy time (every implementor does this — see §4).

Not required to write any bespoke top-level UI screen: the *field-level* rendering is generic (`ConfigField` subtypes + their paired UI drivers), but the *panel-level* container (`ConfigPanel` subclass bound via `Bind(List<EntityConfiguration>, UIScreen)` in `ScheduleOne.Management.UI/ConfigPanel.cs`) is per-`EConfigurableType` and must be authored/prefabbed per type — see §3.

## 3. Full `ManagementClipboard` flow

1. **Equip**: `ManagementClipboard_Equippable.Equip(ItemInstance)` (`ScheduleOne.Tools/ManagementClipboard_Equippable.cs`) opens `ManagementWorldspaceCanvas`, positions the physical clipboard model, sets `ManagementClipboard.Instance.IsEquipped = true`, wires `onOpened`→`FullscreenEnter`/`onClosed`→`FullscreenExit`, and shows the `"clipboard"` (or `"clipboard_heatmaptoggle"`) input-prompt module.
2. **Hover/select world objects**: `ManagementWorldspaceCanvas.Update()` raycasts against `ObjectSelectionLayerMask` (`PlayerCamera.LookRaycast`) and does `hit.collider.GetComponentInParent<IConfigurable>()` to find `HoveredConfigurable`. Left-click (`PrimaryClick`) toggles membership in `SelectedConfigurables`; right-click clears selection. Only objects in `Property.Configurables` within 6m (`VISIBILITY_RANGE`) of the current owned property are candidates (`GetConfigurablesToShow`), plus whatever is already selected/hovered.
3. **Open the interface**: pressing Interact while equipped (`ManagementClipboard_Equippable.Update`, gated by `CanToggleClipboard`) builds `List<IConfigurable>` = `SelectedConfigurables` (+ `HoveredConfigurable` if not already in it) and calls `ManagementClipboard.Instance.Open(list, this)`.
4. `ManagementClipboard.Open` (`ScheduleOne.Tools/ManagementClipboard.cs`) does camera/DoF/mouse setup, `SelectionInfo.Set(selection)`, then delegates the actual panel construction to `Singleton<ManagementInterface>.Instance.Open(selection, equippable)`, and calls `SetConfigurer(Player.Local.NetworkObject)` (a server RPC) on every selected `IConfigurable` — this is the server-authority lock (§5).
5. **UI construction path — per-type, not generic**: `ManagementInterface.Open` (`ScheduleOne.Management/ManagementInterface.cs`) checks `areConfigurablesUniform` (all selected objects share the same `ConfigurableType`); if uniform and non-empty, it looks up a prefab via `GetConfigPanelPrefab(Configurables[0].ConfigurableType)` — a linear search over the Inspector-serialized `ConfigPanelPrefabs` array (`ConfigurableTypePanel[] { EConfigurableType Type; ConfigPanel Panel; }`), instantiates it under `PanelContainer`, and calls `loadedPanel.Bind(Configurables.Select(x => x.Configuration).ToList(), UIScreen)`. **So: each `EConfigurableType` needs its own hand-built `ConfigPanel` prefab/subclass** (e.g. `PotConfigPanel`, `BotanistConfigPanel`) registered in that array — it is emphatically NOT a fully generic reflective panel. What *is* generic/reusable is (a) the field-widget layer (`ItemField`/`ObjectField`/`ObjectListField`/`NPCField`/`RouteListField`/`NumberField`/`StringField`/`QualityField`/`StationRecipeField`, each with its own reusable selector UI screen like `ObjectSelector`, `NPCSelector`, `ItemSelector`, `RecipeSelector`, `TransitEntitySelector`, `StringSetter` referenced on `ManagementInterface`) and (b) `ConfigPanel.Bind`'s screen/selectable wiring (`UIContentPanel`, `UISelectable`) — a mod's custom panel subclass can lean on those.
6. **Close**: `ManagementClipboard.Close()` clears configurer locks (`SetConfigurer(null)` for anything still owned by the local player), tells `ManagementInterface.Close()` to `DestroyConfigPanel()` (destroys the instantiated panel and removes the `UIScreen`), and lerps the clipboard model away.

## 4. `Property.Configurables` / `AddConfigurable` — registration pattern

`ScheduleOne.Property/Property.cs`: `public List<IConfigurable> Configurables` plus:
```csharp
public void AddConfigurable(IConfigurable configurable) { if (!Configurables.Contains(configurable)) Configurables.Add(configurable); }
public void RemoveConfigurable(IConfigurable configurable) { if (Configurables.Contains(configurable)) Configurables.Remove(configurable); }
```
Every one of the 15 implementors self-registers/unregisters at its own lifecycle points, never centrally:
- `Pot.InitializeGridItem(...)`: `base.ParentProperty.AddConfigurable(this);` then builds `potConfiguration`; `Pot.Destroy()`: `base.ParentProperty.RemoveConfigurable(this);`
- Same `AddConfigurable`/`RemoveConfigurable` pairing appears in `MushroomBed.cs`, `ChemistryStation.cs`, `LabOven.cs`, `PlaceableStorageEntity.cs`, `Cauldron.cs`, `DryingRack.cs`, `BrickPress.cs`, `PackagingStation.cs`, `MixingStation.cs`, `MushroomSpawnStation.cs` (grep confirms exact call sites/line numbers listed above).
- Employee variants (`Botanist.cs`, `Cleaner.cs`, `Chemist.cs`, `Packager.cs`) call `prop.AddConfigurable(this)` when assigned to a property and `base.AssignedProperty.RemoveConfigurable(this)` on unassign/destroy.

**Yes, this is the correct integration point for a mod.** `Property.Configurables` is a plain public `List<IConfigurable>`, and `AddConfigurable`/`RemoveConfigurable` are public instance methods with no access restriction — a mod object that implements `IConfigurable` can call `myProperty.AddConfigurable(this)` from its own spawn logic and `RemoveConfigurable` from its own teardown, and it will then show up in `ManagementWorldspaceCanvas.GetConfigurablesToShow()` (which iterates `CurrentProperty.Configurables`) and thus become hoverable/selectable through the vanilla clipboard exactly like any vanilla station.

## 5. Gotchas

- **Server authority / networking is mandatory, not optional.** `IConfigurable.CurrentPlayerConfigurer` is a FishNet `NetworkObject` (in `Pot` it's a `[SyncVar]`, `WritePermission.ServerOnly`), and `SetConfigurer` is a `[ServerRpc(RequireOwnership = false, RunLocally = true)]`. `ManagementClipboard.Open`/`Close` calls `SetConfigurer` on every selected configurable to claim/release a soft lock; `IsBeingConfiguredByOtherPlayer` (`!CurrentPlayerConfigurer.IsOwner`) is polled every frame in `ManagementClipboard.Update()` and forces `Close()` if another player grabs the object mid-edit. Field values propagate through `ConfigurationReplicator` (`NetworkBehaviour` with 18 paired `ServerRpc`/`ObserversRpc` methods, one pair per `ConfigField` subtype). A mod's configurable object and its `EntityConfiguration`/replicator **must** be built on top of FishNet networking (spawned `NetworkObject`s) to behave correctly in multiplayer — this can't be faked client-side.
- **`EConfigurableType` is a closed, non-extensible enum** (`ScheduleOne.Management/EConfigurableType.cs`, 15 members, 1:1 with the 15 implementors) and `ManagementInterface.ConfigPanelPrefabs` is a fixed Inspector-serialized array keyed by that enum, validated at `Awake()` (`Console.LogError($"No config panel prefab assigned for configurable type {eConfigurableType}")` for every enum value with no matching prefab). A brand-new mod-defined configurable type has no legitimate enum slot: either (a) reuse an existing enum value that happens to fit semantically (breaks the "uniform type" grouping/rename/multi-select logic wherever the real vanilla type is also present), or (b) Harmony/IL-patch the enum and inject a new `ConfigurableTypePanel` entry into the live `ConfigPanelPrefabs` array (e.g. patch `ManagementInterface.Awake` postfix to `Array.Resize` and append), or (c) don't use `ManagementInterface`'s panel system at all and instead reuse only the lower-level pieces (individual `ConfigField` types + their selector UI screens) inside a fully custom panel/screen the mod opens itself. Any from-scratch middleware must pick one of these and document the tradeoff explicitly.
- **This system is Property-scoped, not arbitrary-world-object.** `ManagementWorldspaceCanvas.GetConfigurablesToShow()` only surfaces configurables that are (a) in `CurrentProperty.Configurables` where `CurrentProperty = PropertyManager.GetNearestProperty(...)` and `CurrentProperty.IsOwned`, or (b) already hovered/selected directly via raycast. So a van (which travels between/outside properties) or a delivery-drop object not parented under a `Property` will never appear via the "nearby property" branch — it can still be picked up via direct raycast hover (`GetHoveredConfigurable` doesn't check property ownership, just layer mask + `IsBeingConfiguredByOtherPlayer`), but persistent property-Wide listing/culling won't include it. A van-delivery mod wanting the "nearest property" convenience will need its own property association, or must rely purely on direct-hover selection.
- **Selection UX assumes homogeneity.** `ManagementInterface.Open`, `SelectionInfoUI.Set`, and `ManagementWorldspaceCanvas.UpdateInputPrompt` all short-circuit to "mixed types" behavior (no panel, generic label) the moment a multi-select spans more than one `ConfigurableType` — so mixing a mod's new configurable type with a vanilla one in the same click-drag selection is explicitly unsupported UX, not just untested.
- **UI lifecycle**: `ConfigPanel` prefabs are instantiated fresh under `PanelContainer` on every `Open` and destroyed on every `Close` (`ManagementInterface.InitializeConfigPanel`/`DestroyConfigPanel`) — no pooling — and `WorldspaceUIElement` prefabs are instantiated once per configurable via `CreateWorldspaceUI()` and persist until `DestroyWorldspaceUI()`/object destruction; a mod must call `CreateWorldspaceUI()` at spawn and guarantee `DestroyWorldspaceUI()`/`ParentProperty.RemoveConfigurable(this)` at every despawn path (including host-shutdown/scene-unload) or it leaks worldspace UI and stale `Property.Configurables` entries (the code defends against the latter somewhat via `IsDestroyed` checks in `RemoveNullConfigurables`, but that only prunes, it doesn't clean up `Configurables` list membership itself since `IsDestroyed` just checks `Transform == null`).
- **`ManagementClipboard` itself is a `Singleton<ManagementClipboard>`** tightly coupled to one hard-coded equippable tool and one hard-coded `SelectionInfoUI`; there's exactly one clipboard instance and one `ManagementInterface` instance in the scene — a second mod-defined "clipboard-like" tool would either need to reuse this exact singleton (fighting over `Open`/`Close` state with the vanilla clipboard) or fully duplicate the `ManagementClipboard`/`ManagementClipboard_Equippable`/`ManagementWorldspaceCanvas` trio as parallel mod-owned singletons that independently raycast for `IConfigurable` — the latter is more isolated but throws away code reuse for the equip/selection/outline plumbing.agentId: a9c0d19abf27f1ed5 (use SendMessage with to: 'a9c0d19abf27f1ed5', summary: '<5-10 word recap>' to continue this agent)
<usage>subagent_tokens: 128614
tool_uses: 28
duration_ms: 154127</usage>
---

# Agent report: 5_DeliveryApp_UI

# Research Report: DeliveryApp Shop-List Injection & DeliveryShop Order Flow

Source basis: `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.UI.Phone.Delivery/{DeliveryApp,DeliveryShop,ListingEntry}.cs`, `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.UI.Shop/{ShopInterface,ShopListing,ShopColorPicker}.cs`, `/tmp/s1-mod-clean-slate/reference/decompiled/ScheduleOne.UI/App.cs`. Note: this repo contains **decompiled C# only, no Unity scene/prefab YAML dump**, so exact GameObject names/paths in the hierarchy can't be read directly — the plan below is written to be robust to that (uses runtime reflection off existing objects rather than hardcoded paths).

## 1. Plan to add a new working shop tile at runtime via Harmony

**Confirmed data model** (`DeliveryApp.cs:19-63`):
```csharp
[Serializable]
public class DeliveryShopElement { public DeliveryShop Shop; public Button Button; }
private List<DeliveryShop> deliveryShops;              // populated in Awake() via GetComponentsInChildren
[SerializeField] private List<DeliveryShopElement> _shopElements;  // hand-authored in Editor, private, no [HideInInspector]
```

Two independent object graphs matter:
- `deliveryShops` — line 87: `GetComponentsInChildren<DeliveryShop>(includeInactive: true)`, scanned once in `Awake()`. This is just used for lookups (`GetShop`, `Exit`), NOT for rendering.
- `_shopElements` — the only thing `Start()` iterates (lines 101-111) to activate/wire tile buttons. **This is the actual render list.**

**`Start()` wiring loop, exactly (lines 101-111):**
```csharp
foreach (DeliveryShopElement shopElement in _shopElements)
{
    shopElement.Button.gameObject.SetActive(shopElement.Shop.AvailableByDefault);
    shopElement.Button.onClick.AddListener(delegate { OpenShop(shopElement.Shop); });
    DeliveryShop shop = shopElement.Shop;
    shop.OnSelect = (Action<DeliveryShop>)Delegate.Combine(shop.OnSelect, new Action<DeliveryShop>(CloseShop));
    shopElement.Shop.Initialize();
}
```
This is the complete spec for "what makes a tile real": (a) the Button's active state is driven by `Shop.AvailableByDefault`, not left as authored in the prefab; (b) `OnClick` opens the paired `Shop`; (c) `Shop.OnSelect` is wired to `CloseShop` (fires from `DeliveryShop.Initialize()`'s `BackButton` listener, `DeliveryShop.cs:91-94`); (d) `Shop.Initialize()` is called here, not in `DeliveryShop.Awake()` (there is no `Awake()` on `DeliveryShop` — it's a plain `MonoBehaviour`, all setup is manual/external).

**Two separate GameObjects, two separate parents.** `Button` (the clickable tile) and `Shop` (the `DeliveryShop` component, which owns the full order-flow panel: `ListingContainer`, dropdowns, labels, `OrderButton`, `BackButton`) are *not* parent/child of each other in this data structure — they're just paired by reference in `DeliveryShopElement`. The tile Button almost certainly lives in the shop-list grid/scroll container under `shopListCanvas` (per `Start()` line 128-131, `shopListCanvas`/`orderCanvas` are the two sliding panels: `orderCanvas` = one order-flow view, `shopListCanvas` = the list of tiles). The `DeliveryShop` GameObject is a full-screen order panel most likely parented elsewhere (probably as a sibling under the `orderCanvas` region, or directly under the `DeliveryApp`/`AppsCanvas` root) since `GetComponentsInChildren<DeliveryShop>` in `Awake()` scans the whole `DeliveryApp` hierarchy including inactive objects — consistent with each shop panel being toggled active/inactive via `shop.gameObject.SetActive(...)` in `OpenShop`/`CloseShop` (`DeliveryApp.cs:138,145,161`), not being spawned/destroyed.

**Because no scene dump is available, don't hardcode paths — clone off a live existing pair instead.** Concrete Harmony-based steps:

1. **Patch point:** `Harmony.Patch` a **Postfix on `DeliveryApp.Start()`** (or better, patch it to run after `started = true` logic — actually simplest is a Postfix on `Start()` itself, since your clone/injection must happen after vanilla's own `_shopElements` loop has already wired the originals, so you don't collide with iteration order or duplicate-Initialize a shop you didn't mean to touch). At worst, also Postfix `Awake()` if you need `deliveryShops` populated before your clone exists (see step 6).

2. **Get access to the private fields.** `_shopElements` and `deliveryShops` are `private`; use `Traverse` (HarmonyLib) or reflection:
   ```csharp
   var shopElements = Traverse.Create(__instance).Field<List<DeliveryApp.DeliveryShopElement>>("_shopElements").Value;
   var deliveryShopsList = Traverse.Create(__instance).Field<List<DeliveryShop>>("deliveryShops").Value;
   ```

3. **Pick a template pair** — any existing `DeliveryShopElement` in `_shopElements` (index 0 is fine; you only need its GameObjects as clone templates, not its data).
   ```csharp
   var template = shopElements[0];
   ```

4. **Clone the tile Button**, preserving its real parent/layout slot automatically:
   ```csharp
   Button newButton = UnityEngine.Object.Instantiate(template.Button, template.Button.transform.parent);
   newButton.name = "GRQD_ShopButton";
   ```
   `Object.Instantiate(original, parent)` reparents to `parent` and preserves local transform/layout — this is exactly how vanilla itself spawns things into layout containers (`ListingEntry` in `DeliveryShop.Initialize()`, `ListingUI` in `ShopInterface.CreateListingUI`), so it's the game's own idiom, not a guess. Whatever grid/`LayoutGroup`/`ScrollRect` content the template button sits in, your clone lands in the same slot automatically and gets picked up by `DeliveryApp.RefreshLayoutGroupsImmediateAndRecursive` (`DeliveryApp.cs:373-381`) which is called via `RefreshContent()`.
   - Re-tint/re-label the clone: find its `Text`/`TMP_Text` child (walk `newButton.GetComponentInChildren<Text>()` or `TMP_Text`) and set the label to your shop's display name. If the tile has an icon `Image`, set its sprite similarly.
   - Strip old listeners: `newButton.onClick.RemoveAllListeners()` before adding yours (the clone inherits the *editor-wired* listeners if any were serialized directly on the prefab's `Button.onClick` persistent calls — though in this case all real wiring happens in code in `Start()`, so a fresh clone's `onClick` is empty until vanilla's loop or your code adds a listener).

5. **Clone the `DeliveryShop` panel:**
   ```csharp
   DeliveryShop newShop = UnityEngine.Object.Instantiate(template.Shop, template.Shop.transform.parent);
   newShop.name = "GRQD_DeliveryShop";
   ```
   This clones the entire order-flow UI subtree (BackButton, ListingContainer, dropdowns, labels, OrderButton — all serialized `[Header("References")]` fields on `DeliveryShop` point at children of this same GameObject, so they clone correctly as a unit). Then override its public settings fields **before** calling `Initialize()`:
   ```csharp
   newShop.MatchingShopInterfaceName = "YourShopInterfaceName"; // must match a live ShopInterface.ShopName (see §2)
   newShop.ShopColor = yourColor;      // cosmetic only, see §4 — safe to set, nothing reads it
   newShop.AvailableByDefault = true;  // drives newButton.gameObject.SetActive(...) below
   ```
   Also clear any leftover `ListingEntry` children the clone inherited from the template shop (`Initialize()` will populate its own via `MatchingShop.Listings`, but the cloned `ListingContainer` will start with the *template's* already-instantiated `ListingEntry` GameObjects as children — destroy them first: `foreach (Transform t in newShop.ListingContainer) Destroy(t.gameObject);`, since `DeliveryShop` has no cleanup/reset for pre-existing children in `Initialize()`, it just appends).

6. **Register both into the two vanilla collections and wire exactly like vanilla's own loop does:**
   ```csharp
   deliveryShopsList.Add(newShop);   // so DeliveryApp.GetShop()/Exit() find it
   var newElement = new DeliveryApp.DeliveryShopElement { Shop = newShop, Button = newButton };
   shopElements.Add(newElement);

   newButton.gameObject.SetActive(newShop.AvailableByDefault);
   newButton.onClick.AddListener(() => __instance.OpenShop(newShop));
   newShop.OnSelect = (Action<DeliveryShop>)Delegate.Combine(newShop.OnSelect, new Action<DeliveryShop>(__instance.CloseShop));
   newShop.Initialize();
   ```
   This is a byte-for-byte mirror of `DeliveryApp.Start()`'s loop body (lines 103-110) — the exact gap the "previous mod attempt" reportedly missed (it added to `deliveryShops` but skipped all of this `_shopElements` wiring).

7. **Order-of-operations caveat:** Harmony patches typically install before scene `Awake`/`Start` fire (MelonLoader mods init in `OnInitializeMelon`, well before the phone scene loads), so a Postfix on `DeliveryApp.Start()` is the correct, safe hook — it runs after vanilla's own loop has fully wired the real shops, avoiding any interference with `started` guard logic (`DeliveryApp.cs:93-97`, which no-ops re-entry) or the vanilla foreach.

8. **Requires a real `ShopInterface`** to back `newShop.MatchingShopInterfaceName` — see §2, this is unavoidable with vanilla `DeliveryShop.Initialize()` (`DeliveryShop.cs:75-80`): it does `ShopInterface.AllShops.Find(x => x.ShopName == MatchingShopInterfaceName)` and hard-`return`s (logging an error, leaving `listingEntries` empty and `MatchingShop` null) if not found — which would silently break `CanOrder`, `RefreshShop`, etc. since they all dereference `MatchingShop` unguarded (e.g. `WillCartFitInVehicle()` does `MatchingShop.DeliveryVehicle.Vehicle.Storage.SlotCount` with no null check, `DeliveryShop.cs:316`).

## 2. `CanOrder`/`SubmitOrder`/`RefreshShop` — coupling to `ShopInterface`/`ShopListing`

Coupling is **deep and pervasive**, not superficial. Concretely:

- **`Initialize()` (lines 73-102):** the entire cart (`listingEntries`) is built by iterating `MatchingShop.Listings` (a `ShopInterface`'s catalog of `ShopListing`, `ShopInterface.cs:53`) and filtering `listing.CanBeDelivered` (`ShopListing.cs:58`). There is no path to populate `listingEntries` from anything other than a real `ShopInterface`'s catalog — no hook, no virtual method, no event.
- **`GetCartCost()` (lines 388-396)** sums `listingEntry.SelectedQuantity * listingEntry.MatchingListing.Price`, where `Price` (`ShopListing.cs:80-90`) is the catalog's `Item.BasePurchasePrice` or an `OverriddenPrice` — i.e., cost is a *purchase price paid to a shop*, not a "move my own product" concept.
- **`CanOrder()` (lines 258-293)** requires: no active delivery already in flight (`HasActiveDelivery` → `DeliveryManager.GetActiveShopDelivery(this)`), `cartCost + deliveryFee <= player's online balance` (a purchase-affordability check), a selected destination `Property` with loading docks, and `WillCartFitInVehicle()` which reads `MatchingShop.DeliveryVehicle.Vehicle.Storage.SlotCount` (line 316) — i.e. vehicle capacity belongs to the `ShopInterface`, not the `DeliveryShop`.
- **`SubmitOrder()` (lines 123-153)**: builds `StringIntPair` list from cart, calls `NetworkSingleton<VariableDatabase>.NotifyItemAcquired` per item (analytics/quest-hook), constructs a `DeliveryInstance` keyed by `MatchingShopInterfaceName` and `destinationProperty.PropertyCode`, sends it via `NetworkSingleton<DeliveryManager>.Instance.SendDelivery(...)`, records a receipt, and — critically — calls `NetworkSingleton<MoneyManager>.Instance.CreateOnlineTransaction("Delivery from " + MatchingShop.ShopName, -num, ...)`, i.e. it **debits** the player's online balance for `num = GetDeliveryFee() + GetCartCost()`. This is hard-coded purchase semantics (money flows player → shop), the opposite of "move the player's own product" (which would need either no money movement, or money flowing shop → player, or none at all — a transfer, not a purchase).
- **`RefreshShop()` (lines 212-222)** just refreshes UI (`RefreshCart`, `RefreshOrderButton`, destination/loading-dock dropdowns, `DeliveryFeeLabel`) — this part is UI-only and not deeply coupled, it's driven by the same `listingEntries`/`GetCartCost()`/`GetDeliveryFee()` already discussed.
- `Reorder()` / `CanReorder()` / `GetDeliveryCost()` also key everything off `listingEntries.Find(x => x.MatchingListing.Item.ID == item.String)` — same catalog dependency.

**Can Harmony patch `SubmitOrder` cleanly to redirect to custom logic?** Structurally yes, but with real limits:
- None of `DeliveryShop`'s methods (`CanOrder`, `SubmitOrder`, `RefreshShop`, etc.) are `virtual`, and the class isn't designed for subclassing/DI — so you cannot override behavior via inheritance; DeliveryApp always references shops as `DeliveryShop`, not an interface. Your only lever is a Harmony **Prefix that returns `false`** (skip original) on `DeliveryShop.SubmitOrder`/`CanOrder`, scoped to only your shop instance (check `__instance == yourClonedShop`, or check `__instance.MatchingShopInterfaceName == "YourShopName"`), and substitute fully custom logic in the prefix body.
- But even with that prefix in place, you still can't avoid feeding `DeliveryShop.Initialize()` a real `ShopInterface`/`ShopListing` catalog to get `listingEntries` populated in the first place — `ListingEntry` UI (icon, name, price, +/- quantity stepper, lock overlay) is entirely built from `ShopListing`/`StorableItemDefinition` (`ListingEntry.Initialize`, `ListingEntry.cs:32-53`). If "the player's own product" means dynamic, save-specific, quantity-owned items (not a fixed catalog SKU with a `BasePurchasePrice`), you'd need to either (a) synthesize a throwaway `ShopInterface` + one `ShopListing` per distinct product client-side purely to satisfy this UI binding (hacky, and `ShopListing.Price` still reads `Item.BasePurchasePrice`/`OverriddenPrice`, not "how much of this the player owns"), or (b) not use `ListingEntry`/`DeliveryShop`'s cart UI at all.
- `WillCartFitInVehicle()` unconditionally needs `MatchingShop.DeliveryVehicle.Vehicle.Storage.SlotCount` — another mandatory `ShopInterface` dependency with no override point short of also prefixing this method.

**Verdict for §2:** The order-flow methods *can* be patched (Harmony prefix-skip is always technically available), but doing so cleanly for "move the player's own product" semantics means you'd be overriding essentially all of `CanOrder`, `SubmitOrder`, and indirectly needing to fake a `ShopInterface` for `Initialize()`/`WillCartFitInVehicle()` to not NPE — at that point you're rebuilding the entire order flow anyway and just borrowing `DeliveryShop`'s widget layout for it. It's a purchase-catalog component wearing a delivery-flow skin, not a generic "arrange a delivery of arbitrary stuff" component.

## 3. Is `_shopElements` truly Editor-only?

**Confirmed — yes, definitively.** Full-text search of the entire decompiled tree for `_shopElements` (and `DeliveryShopElement`) shows exactly one writer: the Unity Inspector (it's `[SerializeField] private`, no `[HideInInspector]`, no attribute suggesting runtime generation). No method anywhere — not in `DeliveryApp`, not in `DeliveryShop`, not in any other class in the decompiled source — ever does `_shopElements.Add(...)`, `_shopElements = ...`, or otherwise constructs/populates the list at runtime. The only runtime code that touches it is:
- `Start()` (line 101): `foreach (DeliveryShopElement shopElement in _shopElements)` — read-only iteration.
- `SetIsAvailable()` (line 388-393): `_shopElements.Find(...)` — read-only lookup.

There is no `Awake()`-time or lazy-population code path. This fully confirms the prior finding: any new shop tile **must** be injected into this list explicitly (via reflection/Harmony as in §1) — there is no vanilla mechanism that will ever pick up a `DeliveryShop` you merely parent into the hierarchy or add to `deliveryShops`.

## 4. `ShopColor` usage

**It is dead/unused in code — nothing renders it.** `public Color ShopColor;` is declared once at `DeliveryShop.cs:43` and never read anywhere in the entire decompiled codebase (grepped `ShopColor` across all of `/tmp/s1-mod-clean-slate/reference/decompiled`; the only other hits are the unrelated `ShopColorPicker`/`ClothingShopInterface` classes, which are the clothing-shop color-variant picker — a completely different feature, keyed on `EClothingColor`, not this field). No `Image.color = ShopColor` assignment, no `OnValidate`, no consumer in `DeliveryApp` or the tile `Button`. It's a vestigial/artist-facing Inspector field (likely intended so the tile's background/icon color could be hand-tinted to match per-shop in the Editor, set once on the prefab instance manually) with **zero runtime code dependency**. Practically: you can set it on your cloned `DeliveryShop` for parity/consistency with vanilla's data shape, but it buys you nothing visually — if you want your tile tinted, you must find and set the `Image.color` on the cloned Button's child graphic directly (as described in §1 step 4); don't rely on `ShopColor` to do it.

## 5. Recommendation: clone-and-inject, or fully custom phone-app page?

**Split recommendation — clone-and-inject for the shop-list *tile/visibility* mechanism (yes), but do not try to reuse `DeliveryShop`'s order-flow logic unmodified for "move player's own product" (no).**

Reasoning:
- The tile-list problem (§1/§3) is now fully solved and low-risk: it's pure data-structure plumbing (`deliveryShops` + `_shopElements`, both plain `List<T>` reachable via reflection), mirrors an idiom the game itself already uses (`Object.Instantiate(prefab, parent)` into a `LayoutGroup`, refreshed via `DeliveryApp.RefreshLayoutGroupsImmediateAndRecursive`), and requires no engine-level hacks — just a Harmony Postfix on `DeliveryApp.Start()` plus `Traverse` field access. This is straightforward to implement and verify.
- The order-flow problem (§2) is where DeliveryShop stops being a good fit: `CanOrder`/`SubmitOrder`/`WillCartFitInVehicle` are non-virtual and hard-wired to `ShopInterface`/`ShopListing` catalog-purchase semantics (fixed price, online-balance debit, vehicle-capacity-from-ShopInterface, `ListingEntry` UI bound 1:1 to `StorableItemDefinition`+`ShopListing`). Reusing it for "move the player's own product" requires either faking an entire `ShopInterface`/`ShopListing` scaffold (awkward, since `ShopListing.Price` and stock semantics don't map to "quantity the player currently owns") or Harmony-prefixing away nearly every method that matters, at which point you've kept only the visual chrome, not the logic.
- **Practical path:** reuse `DeliveryShop`'s cloned GameObject purely as a **UI shell** (it already has `ListingContainer`, dropdowns for destination/loading dock, an `OrderButton`, labels — all useful, reusable widgets) but Harmony-prefix (`skip original`) `CanOrder`, `SubmitOrder`, and `WillCartFitInVehicle` specifically for your cloned instance(s) (guard on `MatchingShopInterfaceName` or instance identity) with your own logic — still requiring a minimal dummy `ShopInterface` registered in `ShopInterface.AllShops` so `Initialize()` doesn't early-return null (`DeliveryShop.cs:75-80`), backing it with synthetic `ShopListing`s that you keep in sync with the player's actual product inventory (refresh their `Item`/count representation whenever the tile opens, e.g. by overriding `RefreshShop`/hooking `Open()`).
- A **fully custom phone-app page** (new `App<T>` subclass, own `AppIcon`/`HomeScreen` entry, own hand-built UI) avoids all of this coupling entirely and gives full control, but costs you: `DeliveryApp`'s existing shop-list layout/tab/scroll/notification machinery (`TabController`, `MainScrollRect`, `StatusDisplay`/receipt history, the slide transition `DoShopTransitionRoutine`), all of which you'd have to reimplement or omit. Given GRQD specifically wants an entry *in the Delivery app's shop list* (not a standalone app), a standalone page also doesn't satisfy the stated integration goal — it would be a different feature (a new phone app icon), not "an entry in the Delivery shop list."

**Bottom line:** clone-and-inject into `_shopElements`/`deliveryShops` is correct and should proceed as in §1 — that mechanism is solid and fully understood now. For the order logic, don't rely on `DeliveryShop.SubmitOrder`/`CanOrder` as-is; plan for a Harmony prefix-skip override of those specific methods on your instance(s), backed by a minimal synthetic `ShopInterface`/`ShopListing` registration to keep `Initialize()`/`WillCartFitInVehicle()` from null-refing, rather than assuming vanilla purchase semantics apply.agentId: a728e9001884721ea (use SendMessage with to: 'a728e9001884721ea', summary: '<5-10 word recap>' to continue this agent)
<usage>subagent_tokens: 83392
tool_uses: 14
duration_ms: 151121</usage>
---

# Agent report: 6_Singleton_Persistence

# Report: Foundational Middleware Research — ScheduleOne Decompiled Source

## 1. `Singleton<T>` / `NetworkSingleton<T>` / `PlayerSingleton<T>` — exact contract & race conditions

All three live in `ScheduleOne.DevUtilities` and share the identical shape: a `private static T instance`, `public static bool InstanceExists => instance != null`, and `public static T Instance { get; protected set; }`. There is **no null-check, no lock, no lazy-init** inside `Instance.get` — it is a bare static field read. `InstanceExists` is just `instance != null`; it does nothing more than what a middleware wrapper would do itself.

- **`Singleton<T>`** (`ScheduleOne.DevUtilities/Singleton.cs`): plain `MonoBehaviour`. Assignment happens in `Awake()`: if `instance != null` already, it logs a warning and `Destroy`s the *new* gameObject (first-wins semantics), otherwise sets `instance = (T)this`. Cleared to `null` in `OnDestroy()` when `instance == this`.
- **`NetworkSingleton<T>`** (`NetworkSingleton.cs`): a `FishNet.Object.NetworkBehaviour`. Assignment happens in `Awake_UserLogic...` (called from the FishNet-generated `Awake()`), same first-wins pattern but logs "Keeping prior instance reference" instead of destroying (network objects aren't destroyed the same way). Cleared in `OnDestroy()`.
- **`PlayerSingleton<T>`** (`PlayerSingleton.cs`): a plain `MonoBehaviour`, but assignment happens in `OnStartClient(bool IsOwner)` — **not `Awake()`**. If `!IsOwner` the whole component is `Destroy()`ed (non-owning per-player instances of e.g. `PlayerCamera`/`PlayerMovement`/`PlayerInventory` never become the singleton). Only the local owner's component survives and sets `instance`.
- **`PersistentSingleton<T>`** (`PersistentSingleton.cs`) is a thin extension of `Singleton<T>` that additionally does `transform.SetParent(null); DontDestroyOnLoad(gameObject)` in `Awake()` — used by things like `LoadManager`, `SaveManager`, `Lobby`.

**Race condition risk — real and unguarded.** Because assignment happens in Unity lifecycle callbacks (`Awake`, `OnStartClient`) that fire asynchronously relative to scene loads / network spawns, and because `GameManager`, `SaveManager`, `MoneyManager`, etc. are all `NetworkSingleton<T>`/`Singleton<T>` scene objects that only exist after the "Main"/"Tutorial" scene has loaded and (for network ones) after the local client's `OnStartClient` has fired, **any code that runs before that point (mod `OnApplicationStart`, MelonLoader early hooks, menu-scene code) will see `Instance == null` or a stale/destroyed reference from a previous session**. There's also a subtler one: `PlayerSingleton<T>.Instance` can transiently point to nothing until `OnStartClient(IsOwner: true)` runs on the *owner's* object — non-owner player-singleton components are destroyed but that's async too. A middleware should therefore centralize every `.Instance` access behind a helper that checks `InstanceExists` (or non-null) and either throws a descriptive "not ready yet" exception, queues the call, or returns a `bool`/`Try...` pattern — never assume `.Instance` is safe just because some other manager was ready.

## 2. What "fully loaded and safe to touch" actually means

The authoritative readiness signal in vanilla code is `LoadManager` (`ScheduleOne.Persistence/LoadManager.cs`), a `PersistentSingleton<LoadManager>`:

- `LoadManager.IsGameLoaded` (bool) — the single flag vanilla code itself checks everywhere before touching game state post-load (e.g. `Player.cs` `OnStartClient`: `if (Singleton<LoadManager>.Instance.IsGameLoaded) PlayerLoaded(); else onLoadComplete.AddListener(PlayerLoaded);`).
- `LoadManager.IsLoading` (bool) and `LoadManager.LoadStatus` (`enum ELoadStatus { None, LoadingScene, Initializing, LoadingData, SpawningPlayer, WaitingForHost }`) — fine-grained phase. `LoadStatus == ELoadStatus.None` combined with `IsGameLoaded == true` is the real "safe to touch everything" state (only reached after `onLoadComplete.Invoke()` plus a further `yield return new WaitForSeconds(2f)` in `StartGame()`).
- `LoadManager.onLoadComplete` (UnityEvent) — the correct hook point for a middleware's own "ready" event; vanilla itself subscribes to it (see `Player.PlayerLoaded`).
- `LoadManager.IsInGameScene` — `SceneManager.GetActiveScene().name` is `"Main"` or `"Tutorial"` (as opposed to `"Menu"`).
- `Player.Local != null` — set only when the owning client's `Player.OnStartClient` runs (`Local = this;` plus firing `onLocalPlayerSpawned`). `LoadManager.StartGame`'s coroutine literally does `yield return new WaitUntil(() => Player.Local != null);` before proceeding to `ELoadStatus.LoadingData`. Since `PlayerBasePosition`, inventory, camera, etc. all hang off `Player.Local`, this is the second gate.
- Individual `NetworkSingleton<T>.InstanceExists` for whichever managers a piece of code needs (`GameManager`, `MoneyManager`, `TimeManager`, `VariableDatabase`, etc.) — vanilla code guards with `InstanceExists` before unsubscribing/using them in places like `Player.OnDestroy()`.
- The actual per-object data load only completes when the queued `LoadRequest`s in `SaveManager.BaseSaveables`/`LoadManager` finish processing (`while (loadRequests.Count > 0) { ... }` loop in `StartGame`), i.e. **`onLoadComplete` fires after all `IBaseSaveable.Loader.Load()` calls have run**, so it's genuinely the "everything is populated" signal, not just "scene is up."

**Recommended composite readiness check for the middleware:** `LoadManager.InstanceExists && LoadManager.Instance.IsGameLoaded && LoadManager.Instance.LoadStatus == ELoadStatus.None && Player.Local != null` (plus, for any specific manager a call needs, that manager's own `InstanceExists`). Also gate on `LoadManager.Instance.onPreSceneChange` firing to know when to tear back down (vanilla's `SaveManager.Clean()` clears `Saveables`/`BaseSaveables` on that event — a middleware should mirror that lifecycle so it doesn't hold stale references across a scene change/exit-to-menu).

## 3. Save/load pipeline end-to-end, and the extension point for mod data

**Flow:**
1. `LoadManager.StartGame(SaveInfo)` runs a coroutine: stop network, `SceneManager.LoadSceneAsync`, start FishNet transport, wait for `Player.Local != null`, then for every `IBaseSaveable` registered in `SaveManager.Instance.BaseSaveables` (ordered by `IBaseSaveable.LoadOrder`), it does `new LoadRequest(Path.Combine(LoadedGameFolderPath, item.SaveFolderName), item.Loader)`. `LoadRequest` self-queues into `LoadManager.loadRequests`; the coroutine drains up to `LOADS_PER_FRAME = 50` per frame calling `LoadRequest.Complete()` → `Loader.Load(Path)`.
2. Each `Loader` subclass (in `ScheduleOne.Persistence.Loaders`, e.g. `MoneyLoader`, `PlayerLoader`, `GenericSaveablesLoader`) reads a JSON file via `Loader.TryLoadFile`, `JsonUtility.FromJson<T>` into a `SaveData` subclass, wrapped in try/catch logging `Console.LogError` and just skipping on failure (no throw).
3. Saving is the mirror in `SaveManager.Save(path)`: for each `BaseSaveables[i]`, calls `.Save(path)` (default-interface method on `ISaveable` in `Persistence/ISaveable.cs`) which constructs a `SaveRequest(this, parentFolderPath)`. `SaveRequest`'s constructor immediately calls `saveable.GetSaveString()` (wrapped in try/catch) and queues itself; `SaveManager` drains up to `SAVES_PER_FRAME = 15` per frame, calling `SaveRequest.Complete()` → `ISaveable.WriteBaseData()` → `File.WriteAllText(path/SaveFileName.json, saveString)`.
4. `ISaveable` (default-interface-implemented, `Persistence/ISaveable.cs`) is the base contract: `SaveFolderName`, `SaveFileName`, `Loader`, `ShouldSaveUnderFolder`, `LocalExtraFiles`/`LocalExtraFolders` (List<string>) for extra files/subfolders, `HasChanged`, `InitializeSaveable()` (registers with `SaveManager.Instance.RegisterSaveable(this)`), `GetSaveString()`. `IBaseSaveable : ISaveable` just adds `int LoadOrder`.
5. Every concrete `SaveData` (`Persistence.Datas/SaveData.cs`) auto-stamps `DataType = GetType().Name`, `DataVersion` (overridable, default 0), `GameVersion = Application.version` in its constructor — this is the game's own convention for versioned save payloads.

**Clean, already-built extension point for custom mod data:** `IGenericSaveable` / `GenericSaveablesManager` / `GenericSaveData` (`Persistence/IGenericSaveable.cs`, `Persistence/GenericSaveablesManager.cs`, `Persistence.Datas/GenericSaveData.cs`). This is exactly a schemaless key/value bag:
- `GenericSaveData` has `string GUID` plus `List<BoolValue|FloatValue|IntValue|StringValue>` with `Add(key,value)` / `GetBool|GetFloat|GetInt|GetString(key, default)` accessors — a serializable dictionary-of-primitives keyed by string.
- Any object implements `IGenericSaveable { Guid GUID; void Load(GenericSaveData); GenericSaveData GetSaveData(); }` and calls `InitializeSaveable()` (default impl registers with `Singleton<GenericSaveablesManager>.Instance.RegisterSaveable(this)`).
- `GenericSaveablesManager` (itself an `IBaseSaveable`, `SaveFolderName = "GenericSaveables"`) collects `GetSaveData()` from all registered `IGenericSaveable`s into a `GenericSaveablesData` array and writes it as part of the normal save cycle; `GenericSaveablesLoader.Load()` reads it back and dispatches to the matching GUID via `GenericSaveablesManager.LoadSaveable(GenericSaveData)`.

This is vanilla's own designed mechanism for saving "extra" per-object data (used by e.g. custom placed objects) without a custom file format, and it **rides inside the normal save folder** (`<SaveFolder>/GenericSaveables/...`), gets versioned/cleaned/backed-up together with everything else, and survives `SaveManager.ClearBaseLevelOutdatedSaves` because it's a registered `IBaseSaveable`.

**Recommendation for the middleware / GRQD's route config specifically:** Two legitimate options exist, pick based on data shape —
- If the data is naturally per-GUID/per-object (e.g. "this delivery route belongs to this NPC/vehicle/property"), wrap it as `IGenericSaveable` and let it ride inside the vanilla save via `GenericSaveablesManager`. Pro: travels with the save file automatically (same save = same route config, backups/exports include it, multiplayer host owns it). Con: shares vanilla's `GenericSaveData` k/v-primitive-only schema (no nested objects without manual (de)serialization into strings), and it's coupled to `SaveManager`'s save cycle/timing.
- If the data is mod-global or complex/structured (arbitrary nested objects, mod settings, cross-save data), use MelonLoader's own preferences/userdata mechanism (`MelonPreferences` category+JSON, or a bespoke file under `UserData/`) instead — this is *not* "hacking vanilla's save files," it's the standard, sanctioned MelonLoader pattern, and it avoids ever touching `ISaveable`/`SaveManager` internals directly. Given the stated goal ("this library is the ONLY place that touches the game's actual APIs directly"), the middleware should probably standardize on the MelonLoader-file approach as the default for both mods' data, and only reach for `IGenericSaveable` if per-save-slot coupling (route config tied to a specific playthrough) is actually wanted. Either way, the middleware should expose *one* wrapper API for "persist mod data" so callers never touch `SaveManager`/`GenericSaveablesManager` or MelonLoader's IO directly — this is what makes it the sole "genuine" API boundary.

## 4. Version detection at runtime

`UnityEngine.Application.version` is the one and only version string used throughout the codebase (10 usages found), e.g.:
- `SaveData()` constructor stamps `GameVersion = Application.version` on every save payload (`Persistence.Datas/SaveData.cs`).
- `MetadataManager.Awake()` sets `CreationVersion = Application.version` if unset; `GetSaveString()` stamps `MetaData.LastSaveVersion = Application.version` (`DevUtilities/MetadataManager.cs`).
- `LoadManager.StartGame` compares versions for auto-backup: `float versionNumber = SaveManager.GetVersionNumber(info.SaveVersion); float versionNumber2 = SaveManager.GetVersionNumber(Application.version); if (versionNumber < versionNumber2) { ...CreateSaveBackup... }`.
- `SaveManager.GetVersionNumber(string version)` (static, `Persistence/SaveManager.cs`) is the vanilla version-comparison utility: strips `.` and non-digit characters (handles Unity's `f`-suffix build format like `0.3.4f8` → treats `f` as a decimal point), `TrimStart('0')`, `float.TryParse`. It returns `0f` and logs an error on parse failure. A middleware "warn on unexpected version" check can reuse this exact algorithm (or literally call this method via reflection/HarmonyX-free direct call since it's public static) to compare `Application.version` against a known-good version string, rather than doing raw string equality (which breaks on cosmetic suffix differences).
- `GameManager.IS_BETA` (static bool, set from `Settings.cs:105`) and `GameManager.IS_DEMO` (`const bool = false`) — secondary environment flags worth surfacing in the middleware's "environment info" (e.g. beta branches may have divergent APIs; `FeedbackForm.cs` logs `"Beta Branch: " + GameManager.IS_BETA` as precedent for including it in diagnostics).
- No separate internal build-number field exists beyond `Application.version` — that string (Unity Player Settings "Version") is the sole source of truth; there is no MelonLoader-independent build hash/timestamp in vanilla code.

**Recommendation:** middleware readiness/version layer should read `Application.version` once at startup, log it, and compare via the same digit-stripping parse as `SaveManager.GetVersionNumber` against a compiled-in "last verified version" constant, warning (not hard-failing) on mismatch — matching vanilla's own tolerance-based comparison rather than exact string match.

## 5. Cross-cutting patterns the middleware should match, not fight

- **Logging idiom:** `ScheduleOne.Console` (a `Singleton<Console>`, in the `ScheduleOne` root namespace, distinct from `ScheduleOne.DevUtilities`) exposes `static void Log/LogWarning/LogError(object message, UnityEngine.Object context = null)`, each `[HideInCallstack]` and a one-line pass-through to `Debug.Log/LogWarning/LogError`. Every manager, loader, and saveable in the codebase calls `Console.Log(...)` (never raw `Debug.Log` except a handful of low-level spots like `SaveRequest`/`LoadRequest` constructors and `Loader.cs`'s JSON helpers, which use `Debug.LogError` directly — likely because they predate/bypass the console utility). A middleware should route all its own logging through a similarly thin static wrapper (ideally delegating to MelonLoader's own `MelonLogger` for mod-side output, but mirroring this exact "no exceptions, just log and continue" philosophy) rather than throwing.
- **Fail-soft, not fail-fast, on IO/serialization:** virtually every load/save path (`Loader.TryLoadFile`, `Loader.TryDeserialize<T>`, `ISaveable.WriteBaseData`, `SaveRequest` ctor, `LoadRequest.Complete` inside `LoadManager`'s drain loop) wraps `File`/`JsonUtility` calls in try/catch, logs via `Console.LogError`, and continues with a default/empty value rather than propagating the exception. A middleware wrapper around vanilla API calls should adopt the same idiom: catch, log with rich context (what was being accessed, current load state), and return a safe default/`Try...(out T)` rather than letting exceptions bubble into mod code (which two independent mods will not uniformly handle).
- **`Instance`-gating idiom:** code consistently checks `Singleton<X>.InstanceExists` before dereferencing `.Instance` when the object's lifetime is uncertain (see `Player.OnDestroy`, `LoadManager.ExitToMenu`, `Console.SubmitCommand`'s `InstanceFinder.IsHost` checks) but uses bare `Singleton<X>.Instance.Foo` freely once inside code paths gated by `IsGameLoaded`/`OnStartClient`. The middleware should standardize this into a single `TryGet<T>()`/`Require<T>()` helper pair used everywhere internally, so both mods get consistent behavior instead of each writing ad hoc null checks.
- **Static events/delegates for lifecycle signals**, not interfaces: `Player.onLocalPlayerSpawned` (`static Action`), `onPlayerSpawned`/`onPlayerDespawned` (`static Action<Player>`), `LoadManager.onPreSceneChange`/`onPreLoad`/`onLoadComplete`/`onSaveInfoLoaded` (`UnityEvent`), `SaveManager.onSaveStart`/`onSaveComplete` (`UnityEvent`). Subscription/unsubscription is manual and must be paired (`Delegate.Combine`/`Delegate.Remove`, or `AddListener`/`RemoveListener`), and vanilla is careful to unsubscribe in `OnDestroy`/on scene cleanup (`LoadManager.CleanUp()` nulls `Player.onLocalPlayerSpawned`). The middleware's own event surface should follow this exact shape (static C# events or `UnityEvent`s exposed once, with clear add/remove pairing) and — critically — must itself resubscribe to vanilla's static events fresh after every `onPreSceneChange`/`CleanUp()`, since `LoadManager.CleanUp()` wipes several statics (`Player.onLocalPlayerSpawned = null;`) on every load, silently dropping anyone who registered on the old delegate instance without listening for the reset.
- **Registration-by-self-reporting, not lookup:** saveables (`ISaveable`, `IGenericSaveable`) don't get discovered by the manager; they call `InitializeSaveable()` themselves from their own `Awake()`, which internally does `Singleton<SaveManager>.Instance.RegisterSaveable(this)`. If the middleware ever needs to plug mod objects into vanilla's save cycle (rather than using MelonLoader's own file), it must follow this exact self-registration convention at the correct lifecycle point (after `SaveManager`/`GenericSaveablesManager` singleton exists, i.e. gated the same as §2).
- **FishNet ownership/network guards:** code checks `InstanceFinder.IsServer`/`IsClient`/`base.IsOwner`/`base.IsHost` before mutating networked state (`SaveManager.Save()` early-returns `if (!InstanceFinder.IsServer)`; `Player.SaveFolderName` branches on `InstanceFinder.IsServer && base.IsOwner`). Any middleware wrapper around networked managers (`MoneyManager`, `GameManager`, etc., all `NetworkSingleton<T>`) needs to expose/enforce the same client/server/owner distinction rather than assuming single-player, since Schedule I is multiplayer-capable via FishNet + Steam lobbies (`ScheduleOne.Networking.Lobby`).

**Key files cited** (all under `/tmp/s1-mod-clean-slate/reference/decompiled/`):
`ScheduleOne.DevUtilities/Singleton.cs`, `NetworkSingleton.cs`, `PlayerSingleton.cs`, `PersistentSingleton.cs`, `GameManager.cs`, `MetadataManager.cs`, `Extensions.cs`; `ScheduleOne.PlayerScripts/Player.cs`; `ScheduleOne.Persistence/LoadManager.cs`, `SaveManager.cs`, `ISaveable.cs`, `IBaseSaveable.cs`, `IGenericSaveable.cs`, `GenericSaveablesManager.cs`, `LoadRequest.cs`, `SaveRequest.cs`, `SaveInfo.cs`; `ScheduleOne.Persistence.Datas/SaveData.cs`, `GenericSaveData.cs`, `GenericSaveablesData.cs`, `MetaData.cs`, `GameData.cs`; `ScheduleOne.Persistence.Loaders/Loader.cs`, `GenericSaveablesLoader.cs`, `MoneyLoader.cs`, `PlayerLoader.cs`; `ScheduleOne.Networking/Lobby.cs`, `IStaggeredReplicator.cs`; `ScheduleOne/Console.cs`.agentId: ae4cd5b2fc040362b (use SendMessage with to: 'ae4cd5b2fc040362b', summary: '<5-10 word recap>' to continue this agent)
<usage>subagent_tokens: 117406
tool_uses: 39
duration_ms: 148979</usage>
---

