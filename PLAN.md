# Plan

Read `AGENTS.md` first — it has the non-negotiables (no S1API, middleware-interface-only
game access, no narrative comments, ticket-per-change, PR workflow, beta-only). This doc is
the repo-wide roadmap; per-mod/per-feature specs live in `docs/`.

This repo is a monorepo of independently-shippable mods — see `AGENTS.md` for the
OTC/OTCLoader-style structure. Two mods so far: `CleanSlate/` and `DeliveryDriver/`.

## Concept

**Clean Slate:** a storefront system. Dealers work a zone (roughly one per zone) to legally
sell product. Businesses the player owns generate income and cost rent weekly. Full spec:
`docs/clean-slate-spec.md`.

**Delivery Driver ("Fry"):** a hireable logistics NPC that runs repeatable routes moving
product between the player's properties on a schedule. Standalone — works with vanilla,
doesn't require Clean Slate — but optionally supplies Clean Slate's storefront storage when
both are installed. Full spec: `docs/delivery-driver-spec.md`.

Build order: **Delivery Driver first.** It de-risks custom-NPC creation, scheduling, and
product movement — the exact capabilities Clean Slate's storefront/delivery work
(M2 below) will build on — before Clean Slate depends on any of it.

## Milestones

### Delivery Driver

#### DD0 — Foundation ✅

Minimal MelonLoader Il2Cpp plugin scaffold in `DeliveryDriver/`, same pattern as Clean
Slate's M0. Proves the second mod builds and loads independently in the monorepo.

#### DD1 — Own-layer vs. S1API decision

Spec proposed a bake-off (own thin layer vs. S1API's `NPCPrefabBuilder`). Recommendation:
skip the S1API build entirely — see the amendment in `docs/delivery-driver-spec.md`. OTC's
own crash history already shows S1API's custom-NPC code needs runtime IL patching to work,
which is the exact capability this mod needs. Pending final confirmation, but leaning
own-layer-only, no comparison build.

#### DD2 — Driver NPC, routes, payment (build)

Per the spec's task breakdown: spawn Fry with a stable identity id, route data model (up to
5, pickup → dropoff, repeat-cycling), schedule/waypoint van pathing, product load/unload at
each end, daily pay from a designated locker. Not yet broken into tickets.

### Clean Slate

#### M0 — Foundation ✅

Minimal MelonLoader Il2Cpp plugin proving build → deploy → MelonLoader load → the middleware
interface reaching into the game, end to end. Confirmed in-game via a real notification sent
through `Middleware/Notifications.cs`.

#### M1 — Delivery/dock spike ✅

Static feasibility check against the actual game assemblies: can we place a custom dock,
move product between storage locations programmatically, spawn/route a vehicle to a custom
point. All three came back feasible. See `docs/delivery-dock-spec.md`.

#### M2 — Delivery mechanic (build)

Likely builds on Delivery Driver's DD2 output (vehicle spawn/navigate, product movement)
rather than duplicating it — needs revisiting once DD2 exists, not fully speced as
independent work anymore.

- Custom dock component, storage-to-storage transfer, vehicle spawn+navigate — see
  `docs/delivery-dock-spec.md` for the API surface already identified.
- Decision needed: van-only vs. a driver NPC (may just be "reuse Fry").

#### M3 — Dealer storefronts

NPC dealer per zone, selling product on the player's behalf. Not yet speced — needs its own
design pass, especially NPC creation (see DD1 — whatever approach Delivery Driver validates
for custom NPCs is the one this reuses, not a second from-scratch investigation).

#### M4 — Business economy

Owned businesses generate passive income, cost rent weekly. No vanilla rent/passive-income
system exists to hook into (confirmed in the M1 spike) — this is new middleware. Needs an
income-model decision (per-item-sold vs. flat) before build.

## Status

Delivery Driver: DD0 done, DD1 pending final call, DD2 not started.
Clean Slate: M0/M1 done, M2 depends on DD2 landing first, M3/M4 unspeced.

## Open process question

Tickets should live somewhere tools can create/track them and tie directly to PRs. Not yet
resolved which system — flagged in conversation, not blocking the docs/spec work above.
