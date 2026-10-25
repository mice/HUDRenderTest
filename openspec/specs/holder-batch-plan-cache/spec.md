# Holder Batch Plan Cache Specification

## Purpose

Define reuse and invalidation of holder batch partitions and local texture-slot mappings while preserving uncached planning behavior.

## Requirements

### Requirement: Reuse stable holder batch plans

The rendering batch planner SHALL reuse its previous partition and local slot mappings when the ordered holder identities, mesh-data counts, and texture-slot membership are unchanged.

#### Scenario: Stable inputs use the cached plan
- **WHEN** `Plan` is called twice with the same ordered holders and unchanged mesh-data slot values
- **THEN** the second call returns the existing batch partition without rebuilding it and reports a cache hit.

#### Scenario: Geometry-only changes preserve the plan
- **WHEN** holder vertex, UV, color, or transform data changes while holder identity, mesh-data count, and texture-slot values remain unchanged
- **THEN** the next `Plan` call reuses the same partition and local slot mappings.

### Requirement: Invalidate plans when structure changes

The planner SHALL rebuild its partition whenever holder order, holder identity, mesh-data count, or any mesh `TextureIndex` value changes.

#### Scenario: Holder collection changes
- **WHEN** a holder is added, removed, or moved in the input list
- **THEN** the next `Plan` call reports a cache miss and produces a partition for the new ordered collection.

#### Scenario: Texture membership changes
- **WHEN** a mesh data `TextureIndex` changes between plan calls
- **THEN** the next `Plan` call reports a cache miss and recomputes batch membership and local slot mappings.

### Requirement: Preserve partition and slot mapping behavior

Cached and uncached planning SHALL produce the same batch order, holder order, used global slots, and ascending local slot mapping.

#### Scenario: Cached output matches a fresh plan
- **WHEN** equivalent holder inputs are planned once with a warm cache and once by a fresh batcher
- **THEN** both results contain identical batches, holders, used slots, and global-to-local slot mappings.
