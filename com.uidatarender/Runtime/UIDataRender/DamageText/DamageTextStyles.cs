using System;
using UnityEngine;

[Serializable]
public struct DamageTextStyle
{
    public Color32 Color;
    public float PixelSize, Lifetime, Tracking, RisePixels, HorizontalSpread;
    public float PopAmplitude, PopDuration, FadeStart;

    public void Validate()
    {
        if (!DamageTextRequest.Finite(PixelSize) || PixelSize <= 0 || PixelSize > 512 ||
            !DamageTextRequest.Finite(Lifetime) || Lifetime <= 0 || Lifetime > 60 ||
            !DamageTextRequest.Finite(Tracking) || Tracking < 0 ||
            !DamageTextRequest.Finite(RisePixels) || !DamageTextRequest.Finite(HorizontalSpread) ||
            !DamageTextRequest.Finite(PopAmplitude) || PopAmplitude < 0 || PopAmplitude > 4 ||
            !DamageTextRequest.Finite(PopDuration) || PopDuration <= 0 ||
            !DamageTextRequest.Finite(FadeStart) || FadeStart < 0 || FadeStart >= 1)
            throw new ArgumentOutOfRangeException(nameof(DamageTextStyle));
    }
}

[CreateAssetMenu(menuName = "UIData/Damage text styles")]
public sealed class DamageTextStyles : ScriptableObject
{
    public string Version = "damage-style-v1";
    public DamageTextStyle Normal = new DamageTextStyle
    {
        Color = new Color32(255, 244, 203, 255), PixelSize = 24, Lifetime = .8f,
        Tracking = 1, RisePixels = 44, PopAmplitude = .15f, PopDuration = .18f, FadeStart = .65f
    };
    public DamageTextStyle Critical = new DamageTextStyle
    {
        Color = new Color32(255, 166, 32, 255), PixelSize = 33.6f, Lifetime = 1,
        Tracking = 1, RisePixels = 56, HorizontalSpread = 24, PopAmplitude = .45f, PopDuration = .24f, FadeStart = .65f
    };
    public DamageTextStyle Healing = new DamageTextStyle
    {
        Color = new Color32(88, 255, 112, 255), PixelSize = 24, Lifetime = 1.1f,
        Tracking = 1, RisePixels = 48, PopDuration = .2f, FadeStart = .7f
    };

    public DamageTextStyle[] Freeze(out string hash)
    {
        var styles = new[] { Normal, Critical, Healing };
        if (string.IsNullOrEmpty(Version)) throw new InvalidOperationException("A style version is required.");
        foreach (var style in styles) style.Validate();
        hash = DamageTextHash.Compute(writer =>
        {
            writer.Write("damage-style-v1"); writer.Write(Version);
            foreach (var style in styles)
            {
                writer.Write(style.Color.r); writer.Write(style.Color.g); writer.Write(style.Color.b); writer.Write(style.Color.a);
                writer.Write(style.PixelSize); writer.Write(style.Lifetime); writer.Write(style.Tracking);
                writer.Write(style.RisePixels); writer.Write(style.HorizontalSpread); writer.Write(style.PopAmplitude);
                writer.Write(style.PopDuration); writer.Write(style.FadeStart);
            }
        });
        return styles;
    }
}
