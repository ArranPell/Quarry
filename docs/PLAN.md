# Plan

> **Reading this in the public repo?** `docs/DECISIONS.md` (the decision log) and `docs/COMPLETED.md`
> (the completed-phase archive) are cited throughout. They live in my private working repo, along with
> the other archive-only docs. The citations stay as they are: they point at a record that exists, just
> not here.

The work is phased, and phases are grouped into batches. Each batch ends with a build I load-test in
Blish before the next one starts. Each phase's Status line is updated as work lands, and it records what
is true, not what's hoped for.

Nothing is active. The 2026-09-22 editing pass over the public docs is done. It's docs only, with no
change in behaviour, and it ships with the next release. Every phase through Batch H (Phases 0–56) is
done, confirmed in-game and moved to `docs/COMPLETED.md`, where you can search by phase number or batch
name. Milestones, the publish gate and the release loop are in `docs/ROADMAP.md`. This file was cut back
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

## What's next

The 2.0.x maintenance items staged at the top of `docs/BACKLOG.md` (log levels for Sentry, version lines
in the log). The first real bug report will trigger them. Open bugs: `ArranPell/Quarry#3` and `#4`.
Anything else becomes a phase here when it's picked up from BACKLOG or ROADMAP's RC4.

## Backlog

The backlog moved to `docs/BACKLOG.md` (2026-09-06) so ideas can pile up without cluttering this file.
An idea lives there until it's scheduled. Then it becomes a phase here and is deleted from the backlog.
Rejected ideas go to that file's Closed section with a reason. The out-of-scope list is there too.
