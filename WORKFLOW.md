# EliteEnemyVariants GitHub workflow

EliteEnemyVariants uses the same version-branch workflow as RoleShuffle, as
requested by the user on 2026-11-03. These are repository maintenance
instructions, not player-facing release documentation.

## Repository and current branch

- Repository: `https://github.com/CapacityDown/EliteEnemyVariants.git`.
- Integration branch: `main`.
- Current development version: `5.0.0`.
- Current development branch: `version/5.0.0`.
- Keep the existing HTTPS remote and configured Git Credential Manager
  authentication. Do not copy tokens, extract credentials, or change global
  authentication settings.
- The branch was created from the existing `main` commit
  `e74293cffa47ce7732c30cba107a0e4e41c762ac`. Preserve that already-published
  history; do not reset `main` to simulate an earlier release boundary.

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
