# Clean Slate

A from-scratch Schedule I mod, replacing "OverTheCounter" (which we're abandoning due to
accumulated scope creep and an unfixable-from-outside dependency bug).

## Structure

- `vendor/S1API/` — a vendored (forked-in) copy of https://github.com/ifBars/S1API, pulled in via
  `git subtree` so we own the exact code we build against instead of depending on whatever build
  Thunderstore/r2modman happens to have installed. Local fixes live directly in this tree and get
  carried forward across re-syncs via normal git merge. See `docs/updating-s1api.md` for the
  periodic re-sync process.
- `CleanSlate/` — the actual mod project.
