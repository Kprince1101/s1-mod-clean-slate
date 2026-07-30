# S1 Mod Clean Slate

Monorepo of from-scratch Schedule I mods, replacing OverTheCounter (abandoned). Read
`AGENTS.md` for the standing rules and `docs/PLAN.md` for the roadmap before touching anything.

No S1API, no fork of anyone else's community wrapper (KaBooMa's original or the ifBars
fork), in any mod in this repo. Both mods depend on `LegionCore/`, one shared middleware
interface directly against the game's auto-generated `Il2CppScheduleOne.*` interop stubs
(regenerated locally by MelonLoader's Il2CppAssemblyGenerator every launch) plus Harmony —
see `docs/shared-middleware-architecture.md`. Beta branch only.

## Mods

- `CleanSlate/` — storefront/economy mod. Spec: `docs/clean-slate-spec.md`. Status in
  `docs/PLAN.md`.
- `GRQD/` — Global Real Quick Delivery, a van logistics mod (van-only, no driver NPC). Spec:
  `docs/grqd-spec.md`. Status in `docs/PLAN.md`.

Each mod is independently shippable — its own DLL, its own Thunderstore listing, no hard
dependency on the other (same pattern as OTC/OTCLoader). Both share `LegionCore/`.

## Building

Open `S1ModCleanSlate.sln` (repo root) in Rider/Visual Studio — not an individual mod's
`.csproj` directly — so both mods show up in the IDE. Il2Cpp build target is `net6.0`
(matches MelonLoader's actual host runtime). Copy
`LocalPaths.targets.example` (repo root) to `LocalPaths.targets` and fill in your own
profile's `MelonLoader/net6`, `MelonLoader/Il2CppAssemblies`, and `Mods` paths — it's
gitignored and shared by every mod project in the repo. Real builds and in-game
verification happen on your machine, not in CI/sandbox.
