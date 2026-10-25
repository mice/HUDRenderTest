## ADDED Requirements

### Requirement: Reuse stable null texture state

`UIImage` SHALL return texture index -1 without reading the global texture version when the current Sprite and texture are null and the cached null state is already valid.

#### Scenario: Repeated null texture generation
- **WHEN** the same null-Sprite UIImage is generated repeatedly
- **THEN** subsequent generations return -1 without changing geometry or texture registration state.

#### Scenario: Sprite appears after null state
- **WHEN** a non-null Sprite is assigned after repeated null generations
- **THEN** the existing Sprite texture lookup path runs and returns the registered slot.
