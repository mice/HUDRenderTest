# Explicit image generation batches

`UIImage.BeginGenerationBatch(root)` is an opt-in optimization for generating many holder meshes from the same Image template in one synchronous batch. It reuses Canvas lookup (including a missing Canvas), the ancestor check and the root's inverse matrix. Ordinary `DoGenerate` calls outside a scope keep their existing behavior.

```csharp
using (image.BeginGenerationBatch(root))
{
    for (int i = 0; i < meshData.Length; i++)
    {
        image.rectTransform.sizeDelta = sizes[i];
        image.color = colors[i];
        image.DoGenerate(meshData[i], root);
    }
}
```

The caller must keep the parent hierarchy, Canvas availability and root transform stable until the scope is disposed. This includes adding/removing/enabling/disabling a Canvas or reparenting. Start a fresh scope after such changes. Image dimensions, Sprite, color and child transforms remain live and can change between generations. Generation with a different root uses live queries. This API uses Unity objects and must run on the main thread.

Use `using` or `try/finally`. Nested scopes on the same image are rejected. Disposing a copied older scope does not close a newer scope. The scope is a value type; avoid boxing it as `IDisposable` on an allocation-sensitive path. A caller handling multiple images can reuse a scope array, as shown by `HudRcPerformanceRunner` in the Sample.

This scope does not defer work, split meshes or schedule a Job. All requested holders still update in the same frame. It preserves the existing Simple/fallback generation paths and mesh storage.

For performance comparisons, set the Sample's `disableBuildMeshDiagnostics` consistently on both variants. When this option is true, per-holder/Image stage timestamps and aggregation are removed; overall generation and merge/upload timers remain. Disabled stages are marked unavailable rather than zero. Set `useGenerationBatch` to select this optimization; both options are off by default for compatibility.

Correctness and warmed-allocation checks are recorded in `Testing/Unit/UI/UT_UIIMAGE_009.md`, `UT_UIIMAGE_010.md` and `UT_UIIMAGE_011.md`. The repository OpenSpec change `optimize-128-holder-generation` owns the device budget evidence. Results for the 128-holder null-Sprite Simple workload do not replace the broader release acceptance matrix.
