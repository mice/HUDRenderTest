## ADDED Requirements

### Requirement: Repository structure validation
The repository SHALL provide an automated validation command that checks the package/Sample layout and fails with actionable evidence when a structural contract is violated.

#### Scenario: Valid package structure passes
- **WHEN** the package metadata, local Sample link, dependency sources, asmdefs, required assets, and ownership boundaries satisfy the repository contract
- **THEN** structure validation exits successfully and reports the checks performed

#### Scenario: Invalid structure fails clearly
- **WHEN** a required package file, assembly, dependency, asset, or Sample link is missing or inconsistent
- **THEN** structure validation exits non-zero and identifies the failed check and relevant path

### Requirement: Clean-checkout compilation validation
The repository SHALL validate package resolution and compilation from tracked state without using an existing Unity `Library` or ignored dependency directory.

#### Scenario: Clean tracked snapshot compiles
- **WHEN** clean-checkout validation creates or obtains a fresh repository snapshot and opens `HUDRenderTestSample`
- **THEN** Unity resolves dependencies and compiles package and Sample assemblies without errors

#### Scenario: Working Library cannot hide a failure
- **WHEN** the populated working project passes but a clean tracked snapshot fails compilation
- **THEN** overall migration validation fails

### Requirement: EditMode and PlayMode validation
The maintained validation flow SHALL run all package EditMode tests and all Sample PlayMode tests using the assembly boundaries defined by the migration.

#### Scenario: Complete local validation passes
- **WHEN** the repository validation command runs without skip options
- **THEN** all discovered package EditMode and Sample PlayMode tests pass and result XML and logs are produced

#### Scenario: Test failure propagates
- **WHEN** any governed EditMode or PlayMode test fails or no result file is produced
- **THEN** the validation command exits non-zero and reports the failing platform

### Requirement: Coverage gate continuity
The migrated validation flow SHALL retain the `UIDataRender` managed line-coverage gate at the configured minimum threshold.

#### Scenario: Coverage meets threshold
- **WHEN** validation runs with coverage and `UIDataRender` line coverage is at least the configured threshold
- **THEN** the coverage gate passes and preserves the report artifact

#### Scenario: Coverage is below threshold
- **WHEN** measured `UIDataRender` line coverage is below the configured threshold
- **THEN** validation exits non-zero with the measured and required percentages

### Requirement: Test governance consistency
Governed tests and their records SHALL remain unique, bidirectionally linked, correctly scoped, and reproducible by the governance synchronization tool.

#### Scenario: Every test has one record
- **WHEN** governance validation scans package EditMode and Sample PlayMode tests
- **THEN** every governed test has one unique category ID and exactly one existing record at its declared path

#### Scenario: Every record has one test
- **WHEN** governance validation scans package and Sample unit-test records
- **THEN** every active record refers to one existing test and no duplicate or orphan record remains

#### Scenario: Synchronization is idempotent
- **WHEN** governance synchronization runs twice without test changes
- **THEN** the second run produces no repository diff

#### Scenario: Generated paths use current scope
- **WHEN** governance records are generated for package or Sample tests
- **THEN** their test-file paths are relative to the owning package or Sample scope and resolve to existing files

### Requirement: CI validates distributable state
Continuous integration SHALL validate the clean repository state across structure, compilation, EditMode tests, PlayMode tests, and the configured coverage gate.

#### Scenario: Pull request validation succeeds
- **WHEN** a pull request contains a valid package migration state
- **THEN** CI completes structure, clean compilation, EditMode, PlayMode, and coverage checks and uploads their diagnostic artifacts

#### Scenario: Clean-only defect blocks delivery
- **WHEN** a change works only with ignored local files or a pre-existing Unity Library
- **THEN** CI fails before the change is considered deliverable

### Requirement: Active documentation describes current layout
Active repository guidance and automation documentation SHALL use the migrated package and Sample paths.

#### Scenario: Contributor follows documented workflow
- **WHEN** a contributor follows `AGENTS.md`, repository README, package README, or validation instructions from a clean checkout
- **THEN** all referenced source, test, project, and artifact paths resolve to the migrated layout
