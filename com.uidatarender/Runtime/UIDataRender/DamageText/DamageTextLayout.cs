using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public struct DamageTextGlyphQuad
{
    public Vector2 Min, Max;
    public Rect Uv;
}

public static class DamageTextLayout
{
    /// <summary>Centers the complete ink bounds, preserving every bearing and advance.</summary>
    public static int Build(uint amount, DamageTextGlyphAtlas.Snapshot atlas, float tracking,
        byte[] digitScratch, DamageTextGlyphQuad[] output)
    {
        if (amount < 10 || amount > 99999999) throw new ArgumentOutOfRangeException(nameof(amount));
        if (atlas == null || digitScratch == null || digitScratch.Length < 8 || output == null || output.Length < 8)
            throw new ArgumentException("Eight-digit scratch and output buffers are required.");
        int count = 0;
        do { digitScratch[count++] = (byte)(amount % 10); amount /= 10; } while (amount != 0);
        float cursor = 0, minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            var glyph = atlas.GetGlyph(digitScratch[count - 1 - i]);
            if (!glyph.Available) throw new InvalidOperationException("Missing digit glyph.");
            var min = new Vector2(cursor + glyph.Bearing.x, glyph.Bearing.y);
            var max = min + glyph.Size;
            output[i] = new DamageTextGlyphQuad { Min = min, Max = max, Uv = glyph.Uv };
            minX = Mathf.Min(minX, min.x); maxX = Mathf.Max(maxX, max.x);
            minY = Mathf.Min(minY, min.y); maxY = Mathf.Max(maxY, max.y);
            cursor += glyph.Advance + tracking;
        }
        var center = new Vector2((minX + maxX) * .5f, (minY + maxY) * .5f);
        for (int i = 0; i < count; i++) { output[i].Min -= center; output[i].Max -= center; }
        return count;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DamageTextVertex
{
    public const int Stride = 68;
    public Vector2 Position;
    public Color32 Color;
    public Vector2 Uv;
    public Vector4 AnchorAndSpawn, Motion, Animation;
    internal static readonly VertexAttributeDescriptor[] Attributes =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 2),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4)
    };
}

public static class DamageTextAnimation
{
    // The rounded float end time is shared by CPU and Shader, including at large relative times.
    public static bool IsAlive(float time, float spawn, float lifetime) => time >= spawn && time < spawn + lifetime;
    public static float Spread(uint seed)
    {
        unchecked
        {
            seed ^= seed >> 16; seed *= 0x7feb352d; seed ^= seed >> 15; seed *= 0x846ca68b; seed ^= seed >> 16;
            return (seed & 0x00ffffff) / 16777215f * 2f - 1f;
        }
    }
    public static Vector4 Evaluate(float time, float spawn, in DamageTextStyle style, uint seed)
    {
        float age = time - spawn, u = Mathf.Clamp01(age / style.Lifetime);
        float pop = Mathf.Clamp01(age / style.PopDuration);
        float scale = 1 + style.PopAmplitude * Mathf.Sin(Mathf.PI * pop);
        float fade = Mathf.Clamp01((u - style.FadeStart) / (1 - style.FadeStart));
        return new Vector4(Spread(seed) * style.HorizontalSpread * u, style.RisePixels * u,
            scale, IsAlive(time, spawn, style.Lifetime) ? 1 - fade * fade * (3 - 2 * fade) : 0);
    }
}
