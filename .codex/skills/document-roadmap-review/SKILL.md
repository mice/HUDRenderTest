---
name: document-roadmap-review
description: Review and maintain documentation-directory roadmap.md and todo.md for a Unity UPM package project, including TODO lifecycle, package versions, changelog/tag correspondence, and RC-to-stable release evidence. Use only for repositories that develop and deliver a Unity UPM package, not ordinary Unity application projects or other project types.
metadata:
  short-description: Review roadmap, TODO, and releases for a Unity UPM package
---

# Document Roadmap Review

Use this skill when a user asks to check, create, refresh, or assess a Unity UPM package project's `roadmap.md` and `todo.md`.

## Scope lock

This skill handles **one repository per invocation**. Establish the target repository before reviewing or editing project content:

1. If the user specifies a package file or directory, resolve it first and run `git -C <that-directory> rev-parse --show-toplevel` (use a file's containing directory). Otherwise use `git rev-parse --show-toplevel` in the current directory. The resulting repository is the review scope; do not silently substitute the current workspace for an explicitly named target. Filesystem permissions still apply.
2. Confirm the repository identity from its root files (`AGENTS.md`, `README.md`, package metadata, or equivalent), then apply the UPM eligibility gate below before reviewing or editing its roadmap, TODO, versions, or changelog.
3. Do not inspect sibling repositories, parent workspace projects, other packages, or unrelated worktrees merely because they are nearby.
4. Do not turn findings into implementation work. Code defects, missing tests, and infrastructure issues may be recorded in `todo.md`, but they are not fixed unless the user explicitly changes the task to implementation.
5. If the user names multiple repositories, process them as separate, explicitly labeled reviews. Finish the current repository's review before moving to another one.

This scope lock is the primary defense against losing focus while maintaining several libraries.

## Unity UPM eligibility gate

This skill applies **only to Unity UPM package projects**. Explicit invocation does not waive this project-type restriction.

- Identify the package this repository develops and delivers, using its own `package.json` (at the repository root or a documented package subdirectory), UPM-compatible `name` and `version` metadata, and repository documentation or source layout establishing it as the maintained deliverable.
- A package repository may include a sibling Unity sample/test project inside the same repository. The maintained package remains the scope and version authority; the sample project's PlayerSettings version is not the package version.
- A normal Unity application's `Assets/`, `ProjectSettings/`, or `Packages/manifest.json` does not establish UPM eligibility. Installed, cached, vendored, or third-party packages do not make the consuming application a UPM package project. An unrelated npm `package.json` is also insufficient.
- If the project is not a UPM package project, report **not applicable**, briefly cite the project-type evidence, and stop this skill's review without creating or modifying roadmap, TODO, version, or changelog files. Do not report the project as failing UPM governance or convert it into a package.
- If eligibility is unclear, inspect only the minimum local documentation and package metadata needed to establish it; request clarification if it remains unresolved. If several maintained UPM packages exist, use the user-named package or ask which package to review, without expanding to other repositories.
- Record the selected package directory and package name in the review report. Missing package version metadata is a governance defect when other evidence clearly establishes a UPM deliverable, not a reason to silently switch to the sample application's version.

## Required review

Check the four review areas below, applying the node lifecycle rules throughout:

## Roadmap node lifecycle

Treat every roadmap milestone or node as a bounded work cycle. A node is not complete merely because its implementation exists; its TODO review and record update are part of the node's exit criteria.

### Node entry review

Before starting work on a node:

1. Read the entire active `todo.md`, not only items that appear related by title.
2. Assign each relevant open item to the node with an explicit node ID. Mark unrelated items as carried forward, deferred, or belonging to a later node.
3. Detect stale, duplicated, already completed, and newly discovered items. Update the active TODO queue before implementation begins when the user requested maintenance.
4. Record the node's entry review in the roadmap node or its linked evidence, including the TODO snapshot/date and the items committed to the node.
5. Record the node's target release version, or explicitly mark it unassigned. Distinguish the latest released version from the next unreleased target.

### Node exit review

Before marking a node complete:

1. Review the entire `todo.md` again. Do not review only the node's assigned items.
2. For every item assigned to the node, classify it as completed, carried forward, deferred, blocked, or removed as obsolete. Require repository evidence and an acceptance result for `completed`.
3. Add a node completion record to the roadmap node. The record must list every archived TODO ID, its outcome, the evidence or validation reference, and any follow-up item created for a later node.
4. Archive completed entries in the node document or a linked node archive, then remove those completed entries from the active `todo.md`. Do not leave completed checkboxes in the active queue as a substitute for archiving.
5. Keep blocked, deferred, and carried-forward items in `todo.md` with their new node, reason, and next trigger. They are not archived as completed.
6. Recount and re-read the resulting `todo.md` to ensure no archived item remains active and no open item lost its node assignment.
7. Link delivered features and fixes to the relevant changelog entry. An implemented node may be awaiting release; mark it released only after verifying the corresponding Git tag and version metadata under section 4.
8. Re-read live documents and OpenSpec status before writing. Preserve completed or newly archived work from other ongoing tasks. Snapshot counts are historical observations, not a requirement to keep the same tasks open; use new follow-up IDs for new acceptance work and do not rewrite archived changes to add new scope.

The roadmap node is the source of historical completion records. The active `todo.md` is only the current queue. If an archived item is needed later, link to the node record instead of restoring it as an unqualified open item.

### 1. File presence

- Keep `roadmap.md` and `todo.md` in the selected project's documentation directory, never at the repository root. Reuse the documented canonical directory, such as `Documentation~`, `docs`, or `.document`; if multiple directories are plausible and ownership is unclear, ask which one is canonical.
- If no documentation directory exists, ask the user to confirm a dedicated directory path before creating it during authorized maintenance. An earlier explicit directory choice is sufficient confirmation. Do not fall back to root-level documents while waiting. In read-only mode, report the missing directory and proposed location without creating it.
- Search case-insensitively for duplicates such as `Roadmap.md`, `TODO.md`, or nested copies. Report canonical and duplicate locations separately.
- If either file is missing, create it only when the user asked to ensure the files exist. Keep the content tied to evidence from this repository.
- Preserve user edits. Inspect `git status --short` before changing files and do not overwrite modified documents without merging their content carefully.
- When directory migration is in scope, merge unique content and current evidence into the canonical documents, repair relative links and repository references, then remove superseded root copies. Do not leave root-level `roadmap.md`/`todo.md` redirect placeholders; the root README may link to the documentation directory instead. In read-only mode, report the conflict without moving files.

### 2. Roadmap accuracy and usefulness

Read the current roadmap together with repository evidence:

- current source and package layout;
- recent git history and active OpenSpec/change records;
- tests, validation scripts, CI configuration, and generated governance records;
- documented known limitations and risks.

Classify roadmap statements as `current`, `in progress`, `planned`, `blocked`, or `stale`. A completed historical task must not remain presented as future work. Every active roadmap item should state an outcome, a reason or dependency, and a concrete verification or exit condition. Keep future work at the level needed to guide development; do not invent dates, owners, performance numbers, or commitments without repository evidence.

### 3. TODO status

Review every item in `todo.md`:

- `[ ]` means there is remaining work or missing verification.
- `[x]` means repository evidence supports completion.
- Use an explicit `blocked` or `deferred` label when work cannot proceed, with the blocking condition and the next trigger.
- Remove or archive items that are fully complete, duplicated, obsolete, or contradicted by newer evidence.
- Each open item should have an identifier, a short action, an acceptance condition, and a source or affected area when practical.
- Each open item must identify its roadmap node, for example `node: M3` or `node: future`.
- Keep TODO focused on actionable work. Put background, completed history, and rationale in the roadmap or linked evidence.

### 4. Project versions, Git tags, and changelog

Every reviewed project must have an explicit version, and every release version must correspond to a Git release tag and a changelog entry describing its features and fixes.

#### Version baseline

- Use the selected UPM package's `package.json` `version` as the authoritative version; inspect its release tags, changelog, and documented release convention. Dependency versions, sample PlayerSettings versions, and nearby repositories' tags are not the package's version. Any mirrored VERSION file must agree with package metadata.
- Preserve an existing version and tag convention. If metadata is missing but release tags establish a version history, reconcile with that history rather than resetting it.
- If no package version exists in metadata or release history, start at **v0.1.1**. During authorized maintenance, set the selected package's `package.json` `version` to `0.1.1` and identify `v0.1.1` in the roadmap as the initial **unreleased** target. Do not substitute a root VERSION file for the required UPM metadata. In read-only mode, report the missing version and this required starting value without writing files.
- Package formats may store `0.1.1` while the release tag is `v0.1.1`; verify semantic equality using the repository's tag naming convention. Do not arbitrarily bump an existing version or infer a release from a version string alone.

#### Release correspondence

- Inventory the selected package's release tags, including RC tags, and match each to exactly one versioned entry in the package's canonical changelog. Keep `CHANGELOG.md` inside the selected UPM package so it ships with the package; preserve an existing in-package location and naming convention. If the package itself is the repository root, its root changelog is valid. Do not confuse this with the prohibition on root-level roadmap/TODO documents or create a competing repository-level release log.
- For each released tag, check the tagged commit's version metadata and compare the changes since the preceding applicable release with the entry's features and fixes. For the first release, review the delivered baseline. Flag missing or conflicting version metadata and unsupported changelog claims.
- Distinguish local tags from confirmed published tags. Use existing release evidence or remote tag inspection when publication must be established; if unavailable, report publication as unverified rather than claiming it occurred.
- Every release entry must identify the version and exact tag and contain **Features** and **Fixes** sections (or equivalent headings in the repository's language). Describe the delivered behavior and corrected problems, linking relevant roadmap nodes, TODO IDs, issues, or validation evidence where available. If either category has no changes, explicitly say `None`; do not invent changes to fill it.
- An `Unreleased` section or unreleased version entry collects future features and fixes; it does not substitute for a released tag's entry. Only record a release date when supported by release evidence.
- Report both directions of mismatch: a release tag without its changelog entry, and an entry claimed as released without a matching tag. Historical gaps remain explicit follow-up items until evidence supports their repair.
- Record the release commit by resolving the tag and attach it to release evidence. Do not require a committed changelog to contain the hash of its own commit. An empty successful remote tag query establishes only that no tags were advertised at that time, not that no historical release ever occurred.

#### RC validation and stable promotion

- Use RC validation before stable release. For a target `X.Y.Z`, candidate metadata is `X.Y.Z-rc.N` and the default tag is `vX.Y.Z-rc.N`; preserve a documented package-specific tag prefix while maintaining exact version correspondence. Stable metadata/tag are `X.Y.Z` / `vX.Y.Z` under the default convention. The initial stable target remains `0.1.1` when no version history exists; its first candidate is `0.1.1-rc.1`.
- Start at `rc.1` when no candidate exists for that target; otherwise use the next unused monotonically increasing integer. Failed or abandoned published candidates retain their tags, changelog entries, and results. Never overwrite, move, delete, or reuse a published release tag to make a failed candidate appear successful.
- Each RC requires its own versioned changelog entry with Features, Fixes, and validation references. Preparing a candidate does not make it published; publishing an RC does not make it accepted or stable. Track these states separately in roadmap and release evidence.
- Check the project's release acceptance contract: package layout, clean package installation/dependency resolution and compilation, applicable EditMode/PlayMode tests, test governance, configured coverage gate, required performance evidence, and real CI results. Do not invent universal performance numbers, coverage percentages, scene lists, devices, or test counts; use project specifications and explicit user decisions. Missing required criteria become open acceptance tasks, not assumed passes.
- Bind validation evidence to the exact candidate commit/tag. Matching existing results can be reused; results for a different commit do not qualify without a documented applicability assessment and the required reruns. Inspect CI triggers: pushing a tag does not guarantee a run. Use a successful run for the same commit or record the missing exact-ref validation as a release blocker.
- Review performance evidence against the agreed release scope. Where required, verify repeatable benchmarks and representative workloads, environment and load settings, warm-up, CPU/frame-time distributions, allocation, and comparable before/after measurements. Unchanged code must be reported as current-path validation, not improvement. Separate component allocation claims from whole-scene/application allocation.
- If agreed performance gates remain unmet, require measured evidence, a proposed usage budget, residual risks, reopening conditions, and explicit user acceptance of the exception before treating the performance gate as passed. Do not silently relax thresholds or treat an older report's word `accepted` as user confirmation. Required performance gates must pass, or have the agreed exception confirmed, before the first RC is published; scope-changing fixes require relevant revalidation on later RCs.
- After an RC failure, retain the failure evidence, fix the issue in authorized implementation work, prepare the next candidate number, and validate that candidate. Stable promotion requires passing RC evidence and explicit user confirmation referring to that accepted candidate; a general agreement to use RCs is not confirmation of a future untested candidate.
- Stable promotion preserves the accepted RC's functional code, allowing version and release-record changes only. Check the final package metadata/layout and promotion diff, and link stable evidence to the accepted RC and final commit. Functional changes require another RC and its validation before stable promotion.

#### Maintenance and release exit conditions

- During authorized maintenance, create the canonical changelog if missing and add evidence-backed entries or unreleased notes. Track unresolved version/tag/changelog inconsistencies in TODO with a stable ID, roadmap node, acceptance condition, and next trigger.
- Link the roadmap's released and target versions to the canonical version source and changelog. A release is ready only when version metadata, intended tag, changelog features/fixes, and validation evidence agree; the release record is complete only when the tag points to the intended release commit and publication status is verified where claimed.
- Preparing version metadata and changelog entries does not itself authorize creating, moving, deleting, or pushing Git tags, or publishing a release. Perform those actions only when separately included in the user's authorized scope; never rewrite existing release tags merely to repair documentation.
- This skill reviews release readiness; it does not automatically execute benchmarks, fix runtime code, trigger CI, or publish tags. Continue such work only when included in the user's task. A documentation-only review may finish while release blockers remain, but the release node must retain those blockers and cannot be marked accepted or published.

## Editing rules

When updating the documents:

- Keep `roadmap.md` as the stable direction and milestone view.
- Keep `todo.md` as the actionable current queue.
- Link the two documents to each other.
- Give each roadmap node a stable ID and a `TODO lifecycle` or equivalent entry/exit record section. Record archived TODO IDs there when the node closes.
- Keep an archived item's original ID stable; never reuse it for a new task.
- Prefer links to existing evidence over copying long historical documents.
- Preserve the repository's language and existing document style.
- Do not add a generic cross-project backlog. If an item belongs to another library, identify that repository and leave it out of this repository's TODO.

## Completion report

Report:

1. the repository root reviewed and UPM eligibility result, including the selected package directory and name (for an ineligible project, report not applicable and stop);
2. canonical documentation directory, file locations, duplicates, and any root-level roadmap/TODO violations;
3. roadmap status and stale or unsupported claims found;
4. TODO counts by state and the highest-priority open items;
5. current package version, stable target, latest RC number and publication/validation state, latest verified stable tag, package changelog location, and missing or inconsistent release entries;
6. release blockers, required performance/test/CI evidence, and whether any needed budget-exception or stable-promotion confirmation is recorded;
7. files changed and validation performed, distinguishing checks run now from historical evidence.

For a read-only review, do not edit files. For a requested maintenance update, run a whitespace/diff check after editing and show the resulting `git status` scope.
