# Slim Fill Color Check Specification

## Purpose

Preserve Slim fill geometry and color behavior while avoiding boxed color comparisons and managed allocations during warmed filling.

## Requirements

### Requirement: Slim fills preserve color and geometry without boxed comparisons

Slim FillToDrawData and FillWithMatrix SHALL preserve their existing geometry and all-zero color fallback while introducing no managed allocation during warmed calls with sufficient destination capacity.

#### Scenario: Stored color selection
- **WHEN** a stored color has all four channels zero
- **THEN** the output color is opaque white, as in the existing Slim contract.
- **AND** a color with any nonzero channel is copied unchanged, including opaque black and transparent colored values.

#### Scenario: Fill geometry and destination offsets
- **WHEN** Slim geometry has a nonzero shared offset and is appended to a populated destination
- **THEN** positions use the requested translation or matrix, UV texture/flag channels remain intact, and triangle indices are rebased to the destination vertex offset.

#### Scenario: Warmed filling
- **WHEN** either fill method repeatedly appends to preallocated destination lists after warm-up
- **THEN** the call allocates no managed memory and does not box colors for equality.

#### Scenario: Device comparison
- **WHEN** the optimization is evaluated for performance
- **THEN** old/new Slim builds use matching workload and diagnostic configuration, with repeated device reports and source/APK provenance.
- **AND** unavailable device allocation counters remain explicitly unavailable.
