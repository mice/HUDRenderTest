#ifndef UIDATA_DAMAGE_TEXT_ANIMATION
#define UIDATA_DAMAGE_TEXT_ANIMATION
float _DamageTextPlaybackTime;
float4x4 _DamageTextViewProjection;
float4x4 _DamageTextView;
float4 _DamageTextViewport;

float4 DamageTextEvaluate(float spawn, float4 motion, float4 animation)
{
    float age = _DamageTextPlaybackTime - spawn;
    float u = saturate(age / motion.x);
    float pop = saturate(age / animation.y);
    float scale = 1.0 + animation.x * sin(3.141592653589793 * pop);
    float fade = saturate((u - animation.z) / (1.0 - animation.z));
    float alive = _DamageTextPlaybackTime >= spawn && _DamageTextPlaybackTime < spawn + motion.x;
    return float4(motion.z * u, motion.w * u, scale, alive * (1.0 - fade * fade * (3.0 - 2.0 * fade)));
}

float4 DamageTextProject(float2 localPosition, float4 anchor, float4 motion, float4 animation, out float opacity)
{
    float4 state = DamageTextEvaluate(anchor.w, motion, animation);
    float4 clipPosition = mul(_DamageTextViewProjection, float4(anchor.xyz, 1));
    float depth = -mul(_DamageTextView, float4(anchor.xyz, 1)).z;
    opacity = state.w;
    if (depth <= _DamageTextViewport.z || state.w <= 0 || clipPosition.w <= 0)
    {
        opacity = 0;
        return float4(0, 0, 0, 1); // Every vertex becomes the same point, including expired geometry.
    }
    float2 pixels = localPosition * motion.y * state.z + state.xy;
    pixels.y *= _DamageTextViewport.w; // Match GPU projection flips, including RenderTexture targets on D3D.
    clipPosition.xy += pixels * (2.0 / _DamageTextViewport.xy) * clipPosition.w;
    return clipPosition;
}
#endif
