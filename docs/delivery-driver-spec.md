# Delivery Driver ("Fry") — Schedule I Logistics Mod

**Type:** Schedule I content mod (MelonLoader / C#, current game version 0.4.6f8+)
**Status:** Spec / first build target
**Author:** Legion
**Build order:** FIRST. This is the de-risk build — proves custom-NPC + scheduling + product-movement before Clean Slate depends on it.
**Branch target:** beta only. No stable-branch compatibility.

---

## Amendment: Version B (S1API bake-off) — recommend cutting

Own-layer-vs-S1API was proposed as an empirical bake-off. Recommendation: skip Version B,
build own-layer only. We already have first-hand evidence, not a hunch: OTC's crash chain
traced into `PatchS1APIBugs` — IL patches written specifically to work around bugs in
S1API's own custom-NPC code, and the patcher itself crashed inside Mono.Cecil. That's the
exact capability (custom NPC creation) Fry needs. The fragility isn't "stale foundation," a
currently-maintained fork already ships broken-enough custom-NPC code to need runtime IL
patching. A bake-off would spend real time re-discovering what OTC's crash logs already
showed. Final call pending — see repo discussion.

---

## One-line pitch

A hireable delivery driver NPC ("Fry") with a van who runs repeatable routes moving product between your properties on a schedule, paid daily from a locker you designate. Works standalone with vanilla Schedule I and is Clean-Slate-aware.

---

## Design Principle (the whole reason this mod exists the way it does)

**We build our own thin interface layer against the game's own assemblies — not on top of other people's mods.** Third-party mod dependencies (S1API-based mods like OTC) proved fragile and unreliable: they break on version drift and chain you to someone else's maintenance cadence.

Our layer only exposes and interfaces with **exactly what our mods need** — NPC spawn, schedule/pathing, product movement, inventory/locker access. It does **not** try to abstract the entire game. That narrow surface is dramatically more manageable to maintain than a full game-abstraction API, and it means when the game updates, we fix a small, owned layer instead of waiting on anyone.

---

## Two-Version Bake-Off (decide the foundation empirically)

Build the same driver twice, compare, keep the winner. The delivery driver is the ideal low-stakes test case (one NPC, one route system, one pay pickup).

- **Version A — Own thin layer (`LegionS1` or similar).** Built directly against `Assembly-CSharp` for only the internals the driver touches: NPC spawn/appearance, schedule/waypoint pathing, product pickup/dropoff, locker access for pay. No external mod dependency. Full control over version-compat.
- **Version B — On S1API.** Same driver built with S1API's documented `NPCPrefabBuilder` + `WithSchedule`/`WalkTo` pattern. Reference implementation to study: **BigWillyMod** (by ifBars — custom NPC with full schedule/appearance/behavior). Gives the maintained-API path a fair shot.

**Decision criteria:** which is cleaner to write, which survives a game update better, which is less painful to maintain. Whichever wins informs how Clean Slate is built.

**Prior art / why this is de-risked:** custom scheduled NPCs are proven (BigWilly, current). Dealer-behavior modification is proven (High Baller, current, April 2026). The version-drift that killed OTC came from a *stale* foundation, not from these capabilities being impossible.

---

## Character / Aesthetic

- **Name:** Fry
- **Appearance:** resembles Fry from Futurama as closely as the avatar system allows (orange/red hair, white shirt + red jacket vibe, etc.)
- **Van:** Planet Express ship coloring (dark forest green), with a **ROUGH, hand-tagged** version of a Planet-Express-style logo on the van sides.
- **IP note:** Futurama / Planet Express is Fox/Disney IP. The character *resemblance* and *vibe* are homage/parody (lower risk). Keep the logo **loose and hand-tagged, NOT a pixel-perfect copy** — parody framing + rough execution is the safe read. Avoid an exact logo rip.

---

## Core Mechanics (v1.0)

### The driver NPC
- Hireable NPC ("Fry") that spawns and runs on a schedule.
- Custom NPC id must be **stable save data** — set the identity id once and never change it, or it orphans NPCs in saves.
- (If the NPC has a runtime-generated mugshot: note the known S1API bright-flash quirk and add a photosensitivity line to the mod description. May not apply if we render our own.)

### Routes
- Player assigns **up to 5 routes**.
- A route = a pickup location → dropoff location leg (moving product between properties/storage).
- Driver runs routes on **repeat** (cycles through the assigned routes continuously).
- Route execution = drive/walk the van to pickup, load product, travel to dropoff, unload. Schedule/waypoint driven.

### Payment
- Driver is **paid daily** (flat wage, like other workers).
- Pay is pulled **from a locker the player designates** at one of their properties.
- Diegetic touch: pay pickup could be the **first or last delivery on the route** (driver grabs wages from the designated locker as part of a route stop). Nice-to-have if clean; flat daily deduction if not.

### Product movement
- Driver physically moves **the player's produced/stockpiled product** from one location to another. It does not create product — it relocates owned inventory.
- Interfaces with storage/lockers at both ends of a route.

---

## Standalone + Integration

- **Works with vanilla Schedule I** as a general logistics tool — anyone running a multi-property operation wants automated product movement.
- **Clean-Slate-aware** — when Clean Slate is installed, the driver can supply the storefront's on-site storage (weekly restock). But Clean Slate does **not** require this mod (Clean Slate falls back to manual restock), and this mod does **not** require Clean Slate. Optional integration, not a hard dependency either direction.

---

## Open Questions / Design TODO

1. **Van pathing:** does the van actually drive roads, or is it a simplified "walk/teleport with a van model" for reliability? Real vehicle pathing is much harder — decide the fidelity vs. reliability tradeoff early.
2. **Route definition UX:** how does the player set the up-to-5 routes? In-world interaction, a menu, a phone app? (Clean Slate has a phone app — could share.)
3. **Load capacity:** does the van carry a limited amount per trip, or unlimited? Capacity creates interesting logistics; unlimited is simpler.
4. **Pay-from-locker mechanic:** first/last-stop pickup vs. simple daily deduction — pick based on what's clean to implement.
5. **What happens if the pay locker is empty?** (Driver stops? Quits? Warning in app?)
6. **Multiple drivers?** v1.0 = one Fry, or allow hiring several? (Lean one for v1.0.)

---

## v1.0 Scope Line

**In:** one driver (Fry), up to 5 repeatable routes, product movement between locations, daily pay from a designated locker, vanilla-compatible, Clean-Slate-aware, the Fry/Planet-Express aesthetic. Built via the two-version bake-off to settle the foundation question.

**Future:** multiple drivers, load-capacity logistics, route scheduling/timing controls, deeper Clean Slate integration, other vehicle types.

---

## Task Breakdown (for Commander dockets)

**Epic: Delivery Driver (Fry) mod**

- **Docket: Foundation bake-off**
  - Stand up mod project (MelonLoader, current game version target 0.4.6f8)
  - Version A: thin own-layer — spawn a custom NPC against Assembly-CSharp
  - Version B: S1API NPCPrefabBuilder — spawn same NPC (study BigWilly)
  - Compare: cleanliness, update-resilience, maintainability → pick foundation

- **Docket: The driver NPC**
  - Spawn Fry with stable identity id
  - Futurama-Fry appearance
  - Van model + Planet-Express-green + rough tagged logo

- **Docket: Routes**
  - Route data model (pickup → dropoff, up to 5)
  - Repeat cycling through routes
  - Schedule/waypoint pathing for van travel
  - Product load at pickup / unload at dropoff (locker/storage interface)

- **Docket: Payment**
  - Designate pay locker
  - Daily wage deduction (or first/last-stop pickup)
  - Empty-locker handling

- **Docket: Route UX**
  - How the player defines/edits the 5 routes

- **Docket: Integration + ship**
  - Vanilla compatibility pass
  - Clean-Slate-aware hook (optional supply to storefront storage)
  - Test on Vortex against current version
  - Thunderstore publish (IP-safe logo, photosensitivity note if applicable, descriptive tagline)
