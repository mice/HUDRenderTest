using System.Diagnostics;

public static class BuildMeshDiagnostics
{
    public static bool Enabled;
    // Separate opt-in: timestamp reads can dominate these very small writes.
    public static bool ProfileSimpleQuadWrites;

    public static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}

public struct BuildMeshStageTimings
{
    public double SetupMs;
    public double MeshDataInitMs;
    public double DrawDispatchMs;
    public double DoGenerateMs;
    public double SimplePathMs;
    public double SimpleSpriteDataMs;
    public double SimpleDrawingDimensionsMs;
    public double SimpleMeshWriteMs;
    public SimpleQuadWriteTimings SimpleQuadWrite;
    public double UguiFallbackMs;
    public double TransformMs;

    public void Clear()
    {
        SetupMs = 0;
        MeshDataInitMs = 0;
        DrawDispatchMs = 0;
        DoGenerateMs = 0;
        SimplePathMs = 0;
        SimpleSpriteDataMs = 0;
        SimpleDrawingDimensionsMs = 0;
        SimpleMeshWriteMs = 0;
        SimpleQuadWrite.Clear();
        UguiFallbackMs = 0;
        TransformMs = 0;
    }
}

public interface IBuildMeshStageSource
{
    BuildMeshStageTimings LastBuildMeshTimings { get; }
}

public struct SimpleQuadWriteTimings
{
    public int RecordedWrites;
    public double StorageMs;
    public double VertexMs;
    public double UvMs;
    public double ColorMs;
    public double IndexMs;

    public void Clear()
    {
        RecordedWrites = 0;
        StorageMs = VertexMs = UvMs = ColorMs = IndexMs = 0;
    }

    public void Add(SimpleQuadWriteTimings other)
    {
        RecordedWrites += other.RecordedWrites;
        StorageMs += other.StorageMs;
        VertexMs += other.VertexMs;
        UvMs += other.UvMs;
        ColorMs += other.ColorMs;
        IndexMs += other.IndexMs;
    }
}

public interface ISimpleQuadWriteStageSource
{
    SimpleQuadWriteTimings LastSimpleQuadWriteTimings { get; }
}
