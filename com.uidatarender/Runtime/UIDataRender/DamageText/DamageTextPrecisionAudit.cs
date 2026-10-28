using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct DamageTextPrecisionResult
{
    public bool candidatePassed;
    public float maximumPixelError, candidateThreshold;
    public int uniqueLayouts;
    public uint amount;
    public DamageTextKind kind;
    public Vector2 originalLocal, quantizedLocal;
    public float pixelScaleAtPeak;
}

/// <summary>Preparing-only local quantization bound across the complete Clip and each style's visible pop peak.</summary>
public static class DamageTextPrecisionAudit
{
    public static DamageTextPrecisionResult Evaluate(DamageTextClip clip, DamageTextGlyphAtlas.Snapshot atlas, DamageTextStyle[] styles)
    {
        if (clip == null || atlas == null || styles == null || styles.Length != 3) throw new ArgumentException();
        var result = new DamageTextPrecisionResult { candidateThreshold = .25f, candidatePassed = true };
        var seen = new HashSet<ulong>(); var quads = new DamageTextGlyphQuad[8]; var digits = new byte[8];
        for (int i = 0; i < clip.Count; i++)
        {
            var request = clip.GetEvent(i);
            if (!seen.Add(((ulong)request.Amount << 2) | (byte)request.Kind)) continue;
            result.uniqueLayouts++;
            var style = styles[(int)request.Kind]; style.Validate();
            float age = Mathf.Min(style.PopDuration * .5f, style.Lifetime * (1 - 1e-6f));
            float scale = style.PixelSize / atlas.ReferencePixelSize * DamageTextAnimation.Evaluate(age, 0, style, 0).z;
            int count = DamageTextLayout.Build(request.Amount, atlas, style.Tracking, digits, quads);
            for (int q = 0; q < count; q++)
            {
                var quad = quads[q];
                Observe(quad.Min, scale, request, ref result); Observe(quad.Max, scale, request, ref result);
                Observe(new Vector2(quad.Min.x, quad.Max.y), scale, request, ref result);
                Observe(new Vector2(quad.Max.x, quad.Min.y), scale, request, ref result);
            }
        }
        result.candidatePassed = result.maximumPixelError <= result.candidateThreshold;
        return result;
    }
    private static void Observe(Vector2 position, float scale, in DamageTextRequest request, ref DamageTextPrecisionResult result)
    {
        var decoded = new Vector2(Mathf.HalfToFloat(Mathf.FloatToHalf(position.x)), Mathf.HalfToFloat(Mathf.FloatToHalf(position.y)));
        float error = !DamageTextRequest.Finite(decoded.x) || !DamageTextRequest.Finite(decoded.y) ? float.MaxValue : Vector2.Distance(position, decoded) * scale;
        if (error <= result.maximumPixelError) return;
        result.maximumPixelError = error; result.amount = request.Amount; result.kind = request.Kind;
        result.originalLocal = position; result.quantizedLocal = DamageTextRequest.Finite(decoded.x) && DamageTextRequest.Finite(decoded.y) ? decoded : Vector2.zero;
        result.pixelScaleAtPeak = scale;
    }
}
