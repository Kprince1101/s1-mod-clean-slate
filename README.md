# Clean Slate

A from-scratch Schedule I mod. Replaces OverTheCounter, which we're abandoning.

No S1API, no fork of anyone else's community wrapper (KaBooMa's original or the Bars fork). We
write our own middleware interface directly against the game's auto-generated `Il2CppScheduleOne.*`
interop stubs (regenerated locally by MelonLoader's Il2CppAssemblyGenerator every time the game
updates) plus Harmony, built only as big as our actual features need.

## Status: step 1 (prove the pipeline works)

`CleanSlate/` is a minimal MelonLoader Il2Cpp plugin. On load it polls until the game's own
`Il2CppScheduleOne.UI.NotificationsManager` singleton exists, then calls it through the middleware
interface (`CleanSlate/Middleware/Notifications.cs`) to show an in-game notification. This is
deliberately the first thing built: it proves build -> deploy -> MelonLoader load -> the middleware
interface reaching into the game all work, before any real feature code gets written.

No feature code (NPCs, storefronts, dispensary logic) exists yet. Next real feature TBD once step 1
is confirmed working in-game.

## Structure

- `CleanSlate/` — the mod project.
  - `Plugin.cs` — MelonMod entry point.
  - `Middleware/` — the middleware interface: our own layer around raw `Il2CppScheduleOne.*`
    types. Grows one file at a time, only for what a real feature actually needs.
  - `LocalPaths.targets.example` — copy to `LocalPaths.targets` (gitignored) and fill in your
    own MelonLoader/profile paths to build locally.

## Building

This repo's Il2Cpp build target is `net6.0` (matching MelonLoader's actual host runtime) and needs
your own profile's `MelonLoader/net6` and `MelonLoader/Il2CppAssemblies` folders — see
`CleanSlate/LocalPaths.targets.example`.
