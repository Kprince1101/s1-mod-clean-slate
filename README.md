# Clean Slate

A from-scratch Schedule I mod, replacing "OverTheCounter" (which we're abandoning due to
accumulated scope creep and an unfixable-from-outside dependency bug).

## Structure

- `vendor/S1API/` — a vendored (forked-in) copy of https://github.com/KaBooMa/S1API (the ORIGINAL
  S1API, not the "Forked by Bars" community continuation — that fork is what OverTheCounter
  depended on and is what's been crashing). Pulled in via `git subtree` so we own the exact code
  we build against. NOTE: the original has no custom-NPC-creation support at all (no
  network-spawn wrapper, no EnsureMessageConversationReady) — that's Bars-fork-only code. We're
  writing our own version of that feature on top of this clean foundation instead of inheriting
  the Bars fork's version.
- `CleanSlate/` — the actual mod project.
