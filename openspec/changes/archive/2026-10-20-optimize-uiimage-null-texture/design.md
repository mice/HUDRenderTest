## Context

Null Sprite generation is now direct, so texture lookup and holder dispatch are visible in the remaining `BuildMesh` cost. The existing cache handles Sprite-backed textures but has no null-state fast return.

## Goals / Non-Goals

**Goals:** skip redundant null texture cache writes and reuse the holder target Transform during one build.

**Non-Goals:** alter texture slot registration, Sprite-backed cache invalidation, or mesh geometry.

## Decisions

Use explicit null-state identity (`sourceSprite == null`, cached null sprite/texture, cached index -1) before reading `TextureVersion`. A later non-null Sprite or texture naturally misses the existing cache condition. Capture `Target.transform` once before the draw loop.

## Risks / Trade-offs

- [Texture state changes without a Sprite reference change] → A null texture remains index -1 by contract; Sprite-backed paths retain existing version checks.
- [Transform changes during one BuildMesh call] → Existing holder generation already assumes one target transform per call; resolving once preserves that behavior.
