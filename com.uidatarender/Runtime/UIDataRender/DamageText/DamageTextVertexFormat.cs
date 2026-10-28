using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public enum DamageTextPositionFormat { Float32, Float16 }

/// <summary>Explicit 64-byte storage. Only local XY are quantized; all other values are copied.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DamageTextHalfVertex
{
    public const int Stride = 64;
    public ushort PositionX, PositionY;
    public Color32 Color;
    public Vector2 Uv;
    public Vector4 AnchorAndSpawn, Motion, Animation;
    public static DamageTextHalfVertex Encode(in DamageTextVertex vertex) => new DamageTextHalfVertex
    {
        PositionX = Mathf.FloatToHalf(vertex.Position.x), PositionY = Mathf.FloatToHalf(vertex.Position.y),
        Color = vertex.Color, Uv = vertex.Uv, AnchorAndSpawn = vertex.AnchorAndSpawn,
        Motion = vertex.Motion, Animation = vertex.Animation
    };
    public Vector2 DecodePosition() => new Vector2(Mathf.HalfToFloat(PositionX), Mathf.HalfToFloat(PositionY));
    internal static readonly VertexAttributeDescriptor[] Attributes =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 2),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4)
    };
}

/// <summary>Platform seam used only during Preparing, including deterministic unsupported/layout tests.</summary>
public interface IDamageTextVertexFormatSupport
{
    bool SupportsFloat16x2 { get; }
    bool MatchesLayout(Mesh mesh, DamageTextPositionFormat format);
}

public sealed class DamageTextVertexFormatSupport : IDamageTextVertexFormatSupport
{
    public bool SupportsFloat16x2 => SystemInfo.SupportsVertexAttributeFormat(VertexAttributeFormat.Float16, 2);
    public bool MatchesLayout(Mesh mesh, DamageTextPositionFormat format) => DamageTextVertexFormats.MatchesLayout(mesh, format);
}

public static class DamageTextVertexFormats
{
    public static bool MatchesLayout(Mesh mesh, DamageTextPositionFormat format)
    {
        bool half = format == DamageTextPositionFormat.Float16;
        int stride = half ? DamageTextHalfVertex.Stride : DamageTextVertex.Stride;
        int shift = half ? 4 : 0;
        return mesh != null && mesh.GetVertexBufferStride(0) == stride &&
            Marshal.SizeOf(half ? typeof(DamageTextHalfVertex) : typeof(DamageTextVertex)) == stride &&
            Attribute(mesh, VertexAttribute.Position, half ? VertexAttributeFormat.Float16 : VertexAttributeFormat.Float32, 2, 0) &&
            Attribute(mesh, VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 8 - shift) &&
            Attribute(mesh, VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 12 - shift) &&
            Attribute(mesh, VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4, 20 - shift) &&
            Attribute(mesh, VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4, 36 - shift) &&
            Attribute(mesh, VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4, 52 - shift);
    }
    private static bool Attribute(Mesh mesh, VertexAttribute attribute, VertexAttributeFormat format, int dimension, int offset)
        => mesh.HasVertexAttribute(attribute) && mesh.GetVertexAttributeFormat(attribute) == format &&
            mesh.GetVertexAttributeDimension(attribute) == dimension && mesh.GetVertexAttributeOffset(attribute) == offset &&
            mesh.GetVertexAttributeStream(attribute) == 0;

    /// <summary>CPU counterpart of the shared projection, for a quantization-only screen-space error bound.</summary>
    public static Vector2 Project(Vector2 localPosition, in DamageTextRequest request, in DamageTextStyle style,
        float referencePixelSize, float time, Camera camera)
    {
        var animation = DamageTextAnimation.Evaluate(time, request.SpawnTime, style, request.RandomSeed);
        var anchor = camera.WorldToScreenPoint(request.WorldPosition);
        return new Vector2(anchor.x, anchor.y) + localPosition * (style.PixelSize / referencePixelSize * animation.z) +
            new Vector2(animation.x, animation.y);
    }
    public static float ScreenError(Vector2 position, in DamageTextRequest request, in DamageTextStyle style,
        float referencePixelSize, float time, Camera camera)
    {
        var vertex = new DamageTextVertex { Position = position };
        var decoded = DamageTextHalfVertex.Encode(vertex).DecodePosition();
        if (!DamageTextRequest.Finite(decoded.x) || !DamageTextRequest.Finite(decoded.y)) return float.PositiveInfinity;
        return Vector2.Distance(Project(position, request, style, referencePixelSize, time, camera),
            Project(decoded, request, style, referencePixelSize, time, camera));
    }
}
