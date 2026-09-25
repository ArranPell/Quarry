# Plan

> **Reading this in the public repo?** `docs/DECISIONS.md` (the decision log) and `docs/COMPLETED.md`
> (the completed-phase archive) are cited throughout. They live in my private working repo, along with
> the other archive-only docs. The citations stay as they are: they point at a record that exists, just
> not here.

The work is phased, and phases are grouped into batches. Each batch ends with a build I load-test in
Blish before the next one starts. Each phase's Status line is updated as work lands, and it records what
is true, not what's hoped for.

No batch is active. Every phase through Batch J (Phases 0–61) is done, confirmed in-game and moved to
`docs/COMPLETED.md`, where you can search by phase number or batch name. Milestones, the publish gate and the release loop are in `docs/ROADMAP.md`. This file was cut back
to status only on 2026-09-21. The batch summaries it used to repeat are in ROADMAP and COMPLETED.

## Released

- **2.0.0 (2026-09-15).** The publish gate: `ArranPell/Quarry` went public, with one snapshot commit per
  release and Hunt mode off by default.
- **2.0.1 (2026-09-15).** Fixed the cold-install crash in the Target List, a Blish engine stack overflow on
  a wrapped, centred label (BACKLOG, Known quirks / bugs).
- **2.0.2 (2026-09-20).** The three wiki data files are served from our own SSRD namespace
  (`ArranPell.Quarry`, ROADMAP gate item 6), and Quarry has its own corner icons and emblem. We serve the
  files but can't regenerate them, so the mirrors stay frozen at 22 April 2026.
- **2.0.3 (2026-09-21).** Restored the manifest's required `contributors` field. Without it, Blish's repo
  downloader failed. Quarry now installs from the in-game module repository (DECISIONS 2026-09-21).
- **2.0.4 (2026-09-23), pre-release.** Batch I: fixed `ArranPell/Quarry#5` (duplicate ids in
  `account/achievements` left every achievement without progress) and the Sentry logging pass. Also
  carries the 2026-09-22 public-docs editing pass. Public snapshot `cbc3bb8`, submitted to SSRD as a
  pre-release so the #5 reporter can confirm it.
- **2.0.5 (2026-09-23).** 2.0.4 for everyone: version bump only, submitted to SSRD's normal branch,
  because a pre-release version number isn't reused. Public snapshot `7dc575b`.

## What's next

Batch J (Phases 59–61) passed its load-test on 2026-09-24 and is ready to release as 2.0.6 (ROADMAP,
Releasing); it closes `ArranPell/Quarry#6`. Then: open bugs `ArranPell/Quarry#3` and `#4`. Anything else becomes a phase here when it's
picked up from BACKLOG or ROADMAP. The next milestone is 2.1, fresh data and polish (ROADMAP,
settled 2026-09-24); RC4 is now 2.2.

## Backlog

The backlog moved to `docs/BACKLOG.md` (2026-09-06) so ideas can pile up without cluttering this file.
An idea lives there until it's scheduled. Then it becomes a phase here and is deleted from the backlog.
Rejected ideas go to that file's Closed section with a reason. The out-of-scope list is there too.
