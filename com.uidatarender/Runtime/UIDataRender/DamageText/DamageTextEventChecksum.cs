using System;

/// <summary>Allocation-free live-set checksum; the immutable Clip SHA256 supplies full sequence identity.</summary>
public static class DamageTextEventChecksum
{
    public static long Compute(in DamageTextRequest request)
    {
        unchecked
        {
            ulong hash = 14695981039346656037;
            Mix(ref hash, request.EventId); Mix(ref hash, request.Amount); Mix(ref hash, (uint)request.Kind);
            Mix(ref hash, request.RandomSeed); Mix(ref hash, (uint)BitConverter.SingleToInt32Bits(request.SpawnTime));
            Mix(ref hash, (uint)BitConverter.SingleToInt32Bits(request.WorldPosition.x));
            Mix(ref hash, (uint)BitConverter.SingleToInt32Bits(request.WorldPosition.y));
            Mix(ref hash, (uint)BitConverter.SingleToInt32Bits(request.WorldPosition.z));
            return (long)hash;
        }
    }
    private static void Mix(ref ulong hash, uint value) { unchecked { hash = (hash ^ value) * 1099511628211; } }
}
