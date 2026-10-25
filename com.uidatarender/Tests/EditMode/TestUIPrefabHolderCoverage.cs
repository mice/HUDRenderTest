using NUnit.Framework;
using UnityEngine;

public class TestUIPrefabHolderCoverage
{
    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_020.md
    [Test]
    [Category("UT_PREF_020")]
    public void BuildMesh_StagesAggregateAndRespectNestedTimingBounds()
    {
        CheckStageAccounting<UIMeshData>();
        CheckStageAccounting<UIMeshDataX>();
    }

    private static void CheckStageAccounting<T>() where T : class, IUIData, new()
    {
        using (var fixture = new DiagnosticFixture<T>())
        {
            BuildMeshDiagnostics.Enabled = true;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = true;
            // Cold sprite-cache lookup, warm lookup, then null-Sprite direct generation.
            for (int pass = 0; pass < 3; pass++)
            {
                if (pass == 2) fixture.Direct.sprite = null;
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                fixture.Holder.BuildMesh(fixture.Draws);
                double elapsed = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - started);
                var actual = fixture.Holder.LastBuildMeshTimings;
                var direct = fixture.Direct.LastGenerationStageTimings;
                var fallback = fixture.Fallback.LastGenerationStageTimings;
                Assert.IsTrue(fixture.Direct.LastGenerationUsedSimpleMeshFastPath);
                Assert.IsFalse(fixture.Fallback.LastGenerationUsedSimpleMeshFastPath);
                Assert.AreEqual(pass + 1, fixture.Other.GenerateCount);
                Assert.AreEqual(1, actual.SimpleQuadWrite.RecordedWrites);
                // Exact source aggregation detects omission, duplication and stale accumulation.
                Assert.AreEqual(direct.SimplePathMs + fallback.SimplePathMs, actual.SimplePathMs);
                Assert.AreEqual(direct.SimpleSpriteDataMs + fallback.SimpleSpriteDataMs, actual.SimpleSpriteDataMs);
                Assert.AreEqual(direct.SimpleDrawingDimensionsMs + fallback.SimpleDrawingDimensionsMs, actual.SimpleDrawingDimensionsMs);
                Assert.AreEqual(direct.SimpleMeshWriteMs + fallback.SimpleMeshWriteMs, actual.SimpleMeshWriteMs);
                Assert.AreEqual(direct.UguiFallbackMs + fallback.UguiFallbackMs, actual.UguiFallbackMs);
                Assert.AreEqual(direct.TransformMs + fallback.TransformMs, actual.TransformMs);
                Assert.AreEqual(fixture.Direct.LastSimpleQuadWriteTimings, actual.SimpleQuadWrite);
                foreach (double value in new[] { actual.SetupMs, actual.MeshDataInitMs, actual.DrawDispatchMs,
                    actual.DoGenerateMs, actual.SimplePathMs, actual.SimpleSpriteDataMs,
                    actual.SimpleDrawingDimensionsMs, actual.SimpleMeshWriteMs, actual.UguiFallbackMs, actual.TransformMs })
                    Assert.That(value, Is.GreaterThanOrEqualTo(-1e-9).And.LessThan(double.PositiveInfinity));
                // Structural nesting only: no machine-speed threshold or requirement for a positive tick.
                const double rounding = 1e-9;
                Assert.LessOrEqual(actual.SetupMs + actual.MeshDataInitMs + actual.DrawDispatchMs + actual.DoGenerateMs, elapsed + rounding);
                Assert.LessOrEqual(actual.SimplePathMs + actual.UguiFallbackMs, actual.DoGenerateMs + rounding);
                Assert.LessOrEqual(direct.SimpleDrawingDimensionsMs + direct.SimpleMeshWriteMs + direct.TransformMs, direct.SimplePathMs + rounding);
                Assert.LessOrEqual(direct.SimpleSpriteDataMs, direct.SimpleDrawingDimensionsMs + rounding);
                Assert.LessOrEqual(fallback.TransformMs, fallback.UguiFallbackMs + rounding);
            }
            BuildMeshDiagnostics.Enabled = false;
            fixture.Holder.BuildMesh(fixture.Draws);
            Assert.AreEqual(default(BuildMeshStageTimings), fixture.Holder.LastBuildMeshTimings);
            Assert.AreEqual(default(BuildMeshStageTimings), fixture.Direct.LastGenerationStageTimings);
            Assert.AreEqual(default(BuildMeshStageTimings), fixture.Fallback.LastGenerationStageTimings);
            BuildMeshDiagnostics.Enabled = true;
            fixture.Holder.BuildMesh(System.Array.Empty<IUIDrawTarget>());
            var empty = fixture.Holder.LastBuildMeshTimings;
            Assert.AreEqual(0, empty.DoGenerateMs);
            Assert.AreEqual(0, empty.MeshDataInitMs);
            Assert.AreEqual(0, empty.SimplePathMs);
            Assert.AreEqual(0, empty.UguiFallbackMs);
            Assert.AreEqual(0, empty.TransformMs);
            Assert.AreEqual(default(SimpleQuadWriteTimings), empty.SimpleQuadWrite);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_021.md
    [Test]
    [Category("UT_PREF_021")]
    public void BuildMesh_DisabledDiagnostics_PreserveOutputAndAllocateNothingWhenWarm()
    {
        CheckDisabledAllocation<UIMeshData>();
        CheckDisabledAllocation<UIMeshDataX>();
    }

    private static void CheckDisabledAllocation<T>() where T : class, IUIData, new()
    {
        using (var fixture = new DiagnosticFixture<T>())
        {
            foreach (bool nullSprite in new[] { false, true })
            {
                fixture.Direct.sprite = nullSprite ? null : fixture.Sprite;
                BuildMeshDiagnostics.Enabled = true;
                BuildMeshDiagnostics.ProfileSimpleQuadWrites = true;
                fixture.Holder.BuildMesh(fixture.Draws);
                var expected = new MeshOutput(fixture.Holder);
                BuildMeshDiagnostics.Enabled = false;
                for (int i = 0; i < 64; i++) fixture.Holder.BuildMesh(fixture.Draws);
                using (var recorder = Unity.Profiling.ProfilerRecorder.StartNew(
                    Unity.Profiling.ProfilerCategory.Internal, "GC.Alloc", 4096,
                    Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    var control = new byte[4096];
                    System.GC.KeepAlive(control);
                    recorder.Stop();
                    Assert.IsTrue(recorder.Valid);
                    Assert.Greater(recorder.Count, 0, "The allocation recorder must observe a positive control.");
                    recorder.Reset();
                    recorder.Start();
                    for (int i = 0; i < 256; i++) fixture.Holder.BuildMesh(fixture.Draws);
                    recorder.Stop();
                    Assert.AreEqual(0, recorder.Count, "Warmed BuildMesh including direct, fallback and other targets must not allocate.");
                }
                expected.AssertMatches(new MeshOutput(fixture.Holder));
                Assert.AreEqual(default(BuildMeshStageTimings), fixture.Holder.LastBuildMeshTimings);
                Assert.AreEqual(default(BuildMeshStageTimings), fixture.Direct.LastGenerationStageTimings);
                Assert.AreEqual(default(BuildMeshStageTimings), fixture.Fallback.LastGenerationStageTimings);
            }
        }
    }

    private sealed class MeshOutput
    {
        private readonly System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Vector4> uvs = new System.Collections.Generic.List<Vector4>();
        private readonly System.Collections.Generic.List<Color32> colors = new System.Collections.Generic.List<Color32>();
        private readonly System.Collections.Generic.List<int> indices = new System.Collections.Generic.List<int>();
        public MeshOutput(IUIPrefabHolder holder)
        {
            foreach (var mesh in holder.UIMeshDatas)
                mesh.FillToDrawData(vertices, uvs, colors, indices, Vector3.zero);
        }
        public void AssertMatches(MeshOutput actual)
        {
            CollectionAssert.AreEqual(vertices, actual.vertices);
            CollectionAssert.AreEqual(uvs, actual.uvs);
            CollectionAssert.AreEqual(colors, actual.colors);
            CollectionAssert.AreEqual(indices, actual.indices);
        }
    }

    private sealed class DiagnosticFixture<T> : System.IDisposable where T : class, IUIData, new()
    {
        public readonly DataPrefabHolder<T> Holder = new DataPrefabHolder<T>();
        public readonly UIImage Direct, Fallback;
        public readonly StubDrawTarget Other = new StubDrawTarget();
        public readonly IUIDrawTarget[] Draws;
        public readonly Sprite Sprite;
        private readonly Texture2D texture;
        private readonly GameObject owner;
        private readonly bool enabled = BuildMeshDiagnostics.Enabled;
        private readonly bool quad = BuildMeshDiagnostics.ProfileSimpleQuadWrites;
        private readonly bool fast = UIImage.EnableSimpleMeshFastPath;
        public DiagnosticFixture()
        {
            UIImage.EnableSimpleMeshFastPath = true;
            owner = new GameObject("stage diagnostics", typeof(UIPrefabOwner));
            owner.transform.SetPositionAndRotation(new Vector3(7, 3, 0), Quaternion.Euler(0, 0, 17));
            texture = new Texture2D(16, 16);
            Sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f));
            Direct = MakeImage("direct", UnityEngine.UI.Image.Type.Simple);
            Fallback = MakeImage("fallback", UnityEngine.UI.Image.Type.Filled);
            Fallback.fillAmount = .6f;
            Draws = new IUIDrawTarget[] { Direct, Fallback, Other };
            Holder.SetTarget(owner.GetComponent<UIPrefabOwner>());
        }
        private UIImage MakeImage(string name, UnityEngine.UI.Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UIImage));
            go.transform.SetParent(owner.transform, false);
            go.transform.localPosition = new Vector3(2, -3, 0);
            var image = go.GetComponent<UIImage>();
            image.rectTransform.sizeDelta = new Vector2(32, 24);
            image.sprite = Sprite;
            image.type = type;
            image.color = new Color32(11, 23, 35, 47);
            return image;
        }
        public void Dispose()
        {
            foreach (var mesh in Holder.UIMeshDatas) mesh?.Dispose();
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(Sprite);
            Object.DestroyImmediate(texture);
            BuildMeshDiagnostics.Enabled = enabled;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = quad;
            UIImage.EnableSimpleMeshFastPath = fast;
        }
    }

    private sealed class TestHolder : UIPrefabHolder
    {
        public void AssignTarget(UIPrefabOwner owner) => _target = owner;
    }

    private sealed class StubDrawTarget : IUIDrawTarget
    {
        public int GenerateCount { get; private set; }
        public Transform LastRoot { get; private set; }

        public void DoGenerate(IUIData meshData, Transform root = null)
        {
            GenerateCount++;
            LastRoot = root;
            meshData.TextureIndex = 2;
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_014.md
    [Test]
    [Category("UT_PREF_014")]
    public void UseSlim_CreatesExpectedHolderType_AndCanSwitch()
    {
        var ownerGo = new GameObject("owner", typeof(UIPrefabOwner));
        var holderGo = new GameObject("holder", typeof(TestHolder));
        try
        {
            var owner = ownerGo.GetComponent<UIPrefabOwner>();
            var holder = holderGo.GetComponent<TestHolder>();
            holder.AssignTarget(owner);

            holder.UseSlim(true);
            Assert.IsInstanceOf<DataPrefabHolder<UIMeshDataX>>(holder.DataHolder);

            holder.UseSlim(false);
            Assert.IsInstanceOf<DataPrefabHolder<UIMeshData>>(holder.DataHolder);
        }
        finally
        {
            Object.DestroyImmediate(holderGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_015.md
    [Test]
    [Category("UT_PREF_015")]
    public void BuildMesh_InitializesDataHolderAndRefreshesGeneratedMeshes()
    {
        var ownerGo = new GameObject("owner", typeof(UIPrefabOwner));
        var holderGo = new GameObject("holder", typeof(TestHolder));
        try
        {
            var owner = ownerGo.GetComponent<UIPrefabOwner>();
            var holder = holderGo.GetComponent<TestHolder>();
            holder.AssignTarget(owner);
            holder.UseSlim(false);

            var draw = new StubDrawTarget();
            holder.BuildMesh(new IUIDrawTarget[] { draw });

            Assert.NotNull(holder.DataHolder);
            Assert.AreEqual(1, holder.DataHolder.UIMeshDatas.Count);
            Assert.AreEqual(1, draw.GenerateCount);
            Assert.AreSame(owner.transform, draw.LastRoot);
            Assert.AreEqual(2, holder.DataHolder.UIMeshDatas[0].TextureIndex);
        }
        finally
        {
            Object.DestroyImmediate(holderGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_016.md
    [Test]
    [Category("UT_PREF_016")]
    public void NullSafeSetters_DoNotThrowBeforeBuildMesh()
    {
        var holderGo = new GameObject("holder", typeof(TestHolder));
        try
        {
            var holder = holderGo.GetComponent<TestHolder>();

            Assert.DoesNotThrow(() => holder.SetSprite(0, null));
            Assert.DoesNotThrow(() => holder.SetWidth(0, 10));
            Assert.DoesNotThrow(() => holder.SetTextureIndex(0, 1));
        }
        finally
        {
            Object.DestroyImmediate(holderGo);
        }
    }
}
