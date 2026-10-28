using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>Preparation-only canonical, little-endian hashing; never used in per-frame rendering.</summary>
public static class DamageTextHash
{
    public static string Compute(Action<BinaryWriter> write)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "");
        }
    }

    public static void Write(BinaryWriter writer, in DamageTextRequest value)
    {
        writer.Write(value.EventId); writer.Write(value.Amount); writer.Write((byte)value.Kind);
        writer.Write(value.WorldPosition.x); writer.Write(value.WorldPosition.y); writer.Write(value.WorldPosition.z);
        writer.Write(value.SpawnTime); writer.Write(value.RandomSeed);
    }
}
