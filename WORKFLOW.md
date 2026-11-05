# EliteEnemyVariants GitHub workflow

EliteEnemyVariants uses the same version-branch workflow as RoleShuffle, as
requested by the user on 2026-11-03. These are repository maintenance
instructions, not player-facing release documentation.

## Repository and current branch

- Repository: `https://github.com/CapacityDown/EliteEnemyVariants.git`.
- Integration branch: `main`.
- Current development version: `5.0.1`.
- Current development branch: `version/5.0.1`.
- Current game compatibility target: R.E.P.O. `0.5.1`; compile and run the
  compatibility checks against the installed game's managed assemblies.
- Keep the existing HTTPS remote and configured Git Credential Manager
  authentication. Do not copy tokens, extract credentials, or change global
  authentication settings.
- The current branch starts from the completed `5.0.0` integration on `main`,
  `245517a448cc180629cf8656cb108256dc6cfa52`. Preserve the earlier version branch
  and published history; do not reset `main` to simulate a release boundary.

## Ordinary changes

1. Check the working tree, current branch, remote URL, and upstream before work.
   Fetch the remote and use `git pull --ff-only` when the working tree is safe
   to update. If branches diverge, preserve both histories and review the
   difference instead of resetting or force-pushing.
2. Make requested changes on `version/<version>`. Preserve unrelated work.
3. Run checks appropriate to the changes. For MOD code changes, retain the
   existing Release build, packaging, and EEV_TEST deployment requirements.
   Repository-instruction-only changes do not require a new MOD build.
4. Review the diff, stage only relevant files, and commit the completed work.
   Push normally to the matching version branch and verify the remote commit
   matches the local commit. Do not merge every ordinary change into `main`.

## Version changes

When the user requests a new version, verify the outgoing version's completed
work, then merge its branch into `main`. Push and verify `main` before creating
`version/<new-version>` from that updated integration branch. Switch to the new
branch and make version-number changes there. Do not advance the version
without a user request or merge unfinished work merely to complete this sequence.

On 2026-11-03 the user started `5.0.1`. The `5.0.0` Release build 102, package,
and `EliteEnemyVariants.zip` were verified and preserved before integration.
Continue the build counter across versions; do not reset it. Until a player-facing
change is made, keep the existing changelog intact and do not add an empty `5.0.1`
heading or a version-bump entry. Build and deploy development DLLs separately from
release ZIP generation. Keep the verified `5.0.0` ZIP until a new release is ready;
do not bypass the release packager's version/changelog validation.

Preserve previous version branches and release artifacts. Do not delete branches
or rewrite published history as part of an ordinary version change. Repository
protections and approval requirements remain in force; use a required pull
request or ask for direction rather than bypassing them.

## Scope and release files

Use normal commits, pushes, and verified version-transition merges for this
repository. This workflow does not authorize edits to other MODs, destructive
Git operations, repository visibility changes, or automatic third-party messages.

Keep source, approved runtime assets, checks, and package metadata under Git.
Continue excluding credentials, machine-specific configuration, logs, build
outputs, ZIPs, temporary files, and deployment backups using `.gitignore`.
Keep development instructions separate from player-facing README and CHANGELOG
files. Do not add Git workflow details or same-version development history to
those release documents.

## Release-document conventions

Follow RoleShuffle's player-facing changelog style: English only, newest release
first, headings such as `## 5.0.0` without a leading `v`, and the first bullet
immediately below the heading. Separate releases with one blank line. Use concise
action-led statements describing final changes from the previous published release;
keep distinct changes separate and summarize related feature details together.
Preserve published history and keep `4.0.0` limited to `- First Release`.
Do not add within-version development history, document-edit entries, technical
implementation details, test commands, or build numbers. Keep compatibility
version numbers out of release explanations except for game compatibility updates,
following the user's approved wording: `- Updated compatibility for R.E.P.O. v0.5.0.`
Use the newly supported game version for subsequent game compatibility updates.
Do not add version numbers to Stage Flux or RoleShuffle compatibility explanations.

Deliver release archives only as `EliteEnemyVariants.zip`. Preserve previous
archives in backup directories with that same basename; do not create a
version-suffixed delivery filename. For documentation-only changes, reuse the
verified Release DLL, update the package and EEV_TEST documentation, and verify
every ZIP entry against its package source without incrementing the build number.
