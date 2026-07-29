# Clean Slate — Plan

Read `AGENTS.md` first — it has the non-negotiables (no S1API, middleware-interface-only
game access, no narrative comments, ticket-per-change, PR workflow). This doc is the feature
roadmap; per-feature technical specs live in `docs/`.

## Concept

A storefront system: dealers work a zone (roughly one per zone) to legally sell product.
Businesses the player owns generate income and cost rent weekly. Delivery infrastructure
moves product from storage to where it's sold.

## Milestones

### M0 — Foundation ✅

Minimal MelonLoader Il2Cpp plugin proving build → deploy → MelonLoader load → the middleware
interface reaching into the game, end to end. Confirmed in-game via a real notification sent
through `Middleware/Notifications.cs`.

### M1 — Delivery/dock spike ✅

Static feasibility check against the actual game assemblies before designing further: can we
place a custom dock, move product between storage locations programmatically, spawn/route a
vehicle to a custom point. All three came back feasible. See
`docs/delivery-dock-spec.md` for the findings and the API surface identified.

### M2 — Delivery mechanic (build)

Turn the M1 findings into working middleware and one end-to-end delivery flow.

- Custom dock component (`Middleware/Docks.cs`), placeable at a fixed point, Il2Cpp-injected
  MonoBehaviour.
- Storage-to-storage transfer helper (`Middleware/StorageTransfer.cs`) over `ItemSlot`.
- Vehicle spawn + AI-navigate wrapper (`Middleware/DeliveryVehicles.cs`) over
  `VehicleManager`/`VehicleAgent`.
- Integration: dock → transfer → van pickup as one tested flow.
- Decision needed before this ships: van-only vs. a driver NPC in v1 (see open questions in
  the spec doc).

### M3 — Dealer storefronts

NPC dealer per zone (roughly), able to legally sell product on the player's behalf. Depends
on M2 for getting product to the dealer.

- Not yet speced. Needs its own spike/design pass before build: what NPC-creation surface
  exists post-S1API (S1API's custom-NPC code was the actual crash source in the abandoned OTC
  mod — we're not carrying that code over, so this needs its own from-scratch investigation).
- Zone definition — what "a zone" maps to in the game's own geography/property system.
- Sell-on-player's-behalf transaction flow.

### M4 — Business economy

Owned businesses generate passive income; cost rent weekly.

- Vanilla `Business`/`Property` already expose `MinsPass`/`TimeSkipped` hooks and a save/load
  pipeline — no existing rent or passive-income system found in the vanilla assemblies (see
  spike findings), so this is new middleware, not a hook into something that already exists.
- Needs a decision on income model (per-item-sold vs. flat) before build.

## Status

M0 and M1 done. M2 is next up once picked up as real work. M3/M4 are unspeced — each needs
its own short design pass (mirroring what M1 did for delivery) before any ticket gets filed
for build work.

## Open process question

Tickets should live somewhere tools can create/track them and tie directly to PRs. Not yet
resolved which system — flagged in conversation, not blocking the docs/spec work above.
