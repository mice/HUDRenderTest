using UnityEngine;

[CreateAssetMenu(menuName = "HUD Sample/Damage text clip")]
public sealed class DamageTextClipAsset : ScriptableObject
{
    public string Version = "sample-clip-v1";
    public uint Seed = 12345;
    public float Duration = 10;
    public DamageTextRequest[] Events;
    public DamageTextCameraKey[] CameraKeys;
    public DamageTextClip Freeze() => new DamageTextClip(Events, Duration, Version, Seed, CameraKeys);
}
