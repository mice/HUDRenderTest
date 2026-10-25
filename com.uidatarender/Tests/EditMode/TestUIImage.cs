using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestUIImage
{
    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_011.md
    [Test]
    [Category("UT_UIIMAGE_011")]
    public void GenerationBatch_WarmScopesAndGeneration_DoNotAllocate()
    {
        var parent = new GameObject("allocation parent", typeof(RectTransform), typeof(Canvas));
        bool oldDiagnostics = BuildMeshDiagnostics.Enabled;
        try
        {
            parent.SetActive(false);
            imageObject.transform.SetParent(parent.transform, false);
            var image = imageObject.AddComponent<UIImage>();
            var root = parent.transform;
            BuildMeshDiagnostics.Enabled = false;
            using (var data = new UIMeshDataX())
            {
                for (int i = 0; i < 32; i++)
                    using (image.BeginGenerationBatch(root)) image.DoGenerate(data, root);
                using (var recorder = Unity.Profiling.ProfilerRecorder.StartNew(
                    Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 4096,
                    Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    var control = new byte[4096];
                    System.GC.KeepAlive(control);
                    recorder.Stop();
                    Assert.IsTrue(recorder.Valid);
                    Assert.Greater(recorder.Count, 0, "Positive allocation control must be visible.");
                    recorder.Reset();
                    recorder.Start();
                    for (int i = 0; i < 128; i++)
                        using (image.BeginGenerationBatch(root)) image.DoGenerate(data, root);
                    recorder.Stop();
                    Assert.AreEqual(0, recorder.Count);
                }
            }
        }
        finally
        {
            BuildMeshDiagnostics.Enabled = oldDiagnostics;
            imageObject.transform.SetParent(null, false);
            Object.DestroyImmediate(parent);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_009.md
    [Test]
    [Category("UT_UIIMAGE_009")]
    public void GenerationBatch_MatchesLiveGeneration_AcrossDynamicInputsAndNewHierarchies()
    {
        var parent = new GameObject("batch parent", typeof(RectTransform), typeof(Canvas));
        var referenceObject = new GameObject("reference", typeof(RectTransform));
        try
        {
            var image = imageObject.AddComponent<UIImage>();
            var reference = referenceObject.AddComponent<UIImage>();
            referenceObject.transform.SetParent(parent.transform, false);
            imageObject.transform.SetParent(parent.transform, false);
            for (int phase = 0; phase < 3; phase++)
            {
                parent.SetActive(phase != 0);
                var canvas = parent.GetComponent<Canvas>();
                canvas.renderMode = phase == 2 ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = true;
                parent.transform.SetPositionAndRotation(new Vector3(phase * 7, 3, 0), Quaternion.Euler(0, 0, phase * 11));
                CompareBatch<UIMeshData>(image, reference, parent.transform, sprite);
                CompareBatch<UIMeshDataX>(image, reference, parent.transform, sprite);
            }
            imageObject.transform.SetParent(null, false);
            referenceObject.transform.SetParent(null, false);
            CompareBatch<UIMeshDataX>(image, reference, parent.transform, sprite);
        }
        finally
        {
            imageObject.transform.SetParent(null, false);
            Object.DestroyImmediate(referenceObject);
            Object.DestroyImmediate(parent);
        }
    }

    private static void CompareBatch<T>(UIImage image, UIImage reference, Transform root, Sprite source)
        where T : class, IUIData, new()
    {
        var actual = new T();
        var expected = new T();
        try
        {
            using (image.BeginGenerationBatch(root))
            {
                for (int i = 0; i < 4; i++)
                {
                    image.type = reference.type = i == 3 ? Image.Type.Filled : Image.Type.Simple;
                    image.rectTransform.sizeDelta = reference.rectTransform.sizeDelta = new Vector2(13.25f + i, 4.5f);
                    image.rectTransform.localPosition = reference.rectTransform.localPosition = new Vector3(i, 3, 0);
                    image.rectTransform.localRotation = reference.rectTransform.localRotation = Quaternion.Euler(0, 0, i * 9);
                    image.color = reference.color = i == 0 ? default(Color32) : new Color32(10, 20, 30, 150);
                    image.sprite = reference.sprite = i % 2 == 0 || i == 3 ? source : null;
                    image.DoGenerate(actual, root);
                    reference.DoGenerate(expected, root);
                    var av = new System.Collections.Generic.List<Vector3>();
                    var au = new System.Collections.Generic.List<Vector4>();
                    var ac = new System.Collections.Generic.List<Color32>();
                    var ai = new System.Collections.Generic.List<int>();
                    var ev = new System.Collections.Generic.List<Vector3>();
                    var eu = new System.Collections.Generic.List<Vector4>();
                    var ec = new System.Collections.Generic.List<Color32>();
                    var ei = new System.Collections.Generic.List<int>();
                    actual.FillToDrawData(av, au, ac, ai, Vector3.zero);
                    expected.FillToDrawData(ev, eu, ec, ei, Vector3.zero);
                    Assert.AreEqual(ev.Count, av.Count);
                    for (int j = 0; j < av.Count; j++) Assert.LessOrEqual(Vector3.Distance(ev[j], av[j]), 1e-4f);
                    CollectionAssert.AreEqual(eu, au);
                    CollectionAssert.AreEqual(ec, ac);
                    CollectionAssert.AreEqual(ei, ai);
                }
            }
        }
        finally { actual.Dispose(); expected.Dispose(); }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_010.md
    [Test]
    [Category("UT_UIIMAGE_010")]
    public void GenerationBatch_RejectsNesting_AndStaleDisposeDoesNotCloseNewBatch()
    {
        var image = imageObject.AddComponent<UIImage>();
        Assert.Throws<System.ArgumentNullException>(() => image.BeginGenerationBatch(null));
        var first = image.BeginGenerationBatch(image.transform);
        try { Assert.Throws<System.InvalidOperationException>(() => image.BeginGenerationBatch(image.transform)); }
        finally { first.Dispose(); }
        using (image.BeginGenerationBatch(image.transform))
        {
            first.Dispose();
            Assert.Throws<System.InvalidOperationException>(() => image.BeginGenerationBatch(image.transform));
        }
        using (image.BeginGenerationBatch(image.transform)) { }
        default(UIImage.GenerationBatch).Dispose();
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_008.md
    [Test]
    [Category("UT_UIIMAGE_008")]
    public void SpriteHolder_FastAndFallbackMatchForManagedAndSlimUnderParentTransform()
    {
        bool oldEnabled = BuildMeshDiagnostics.Enabled;
        bool oldQuad = BuildMeshDiagnostics.ProfileSimpleQuadWrites;
        bool oldFast = UIImage.EnableSimpleMeshFastPath;
        var parent = new GameObject("sprite parent", typeof(UIPrefabOwner));
        try
        {
            parent.transform.SetPositionAndRotation(new Vector3(3, -4, 0), Quaternion.Euler(0, 0, 27));
            imageObject.transform.SetParent(parent.transform, false);
            imageObject.transform.localPosition = new Vector3(2, 7, 0);
            imageObject.transform.localScale = new Vector3(2, .5f, 1);
            var image = imageObject.AddComponent<UIImage>();
            image.sprite = sprite;
            image.color = new Color32(15, 30, 90, 170);
            CheckHolderDiagnostics<UIMeshData>(parent.GetComponent<UIPrefabOwner>(), image);
            CheckHolderDiagnostics<UIMeshDataX>(parent.GetComponent<UIPrefabOwner>(), image);
        }
        finally
        {
            imageObject.transform.SetParent(null, false);
            Object.DestroyImmediate(parent);
            BuildMeshDiagnostics.Enabled = oldEnabled;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = oldQuad;
            UIImage.EnableSimpleMeshFastPath = oldFast;
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_007.md
    [Test]
    [Category("UT_UIIMAGE_007")]
    public void NullSpriteHolder_DiagnosticsAggregate_AndClearAfterFallbackOrDisable()
    {
        bool oldEnabled = BuildMeshDiagnostics.Enabled;
        bool oldQuad = BuildMeshDiagnostics.ProfileSimpleQuadWrites;
        bool oldFast = UIImage.EnableSimpleMeshFastPath;
        var ownerObject = new GameObject("diagnostics owner", typeof(UIPrefabOwner));
        try
        {
            ownerObject.transform.position = new Vector3(4, -3, 0);
            ownerObject.transform.rotation = Quaternion.Euler(0, 0, 15);
            imageObject.transform.SetParent(ownerObject.transform, false);
            imageObject.transform.localPosition = new Vector3(2, 5, 0);
            var image = imageObject.AddComponent<UIImage>();
            image.type = Image.Type.Simple;
            CheckHolderDiagnostics<UIMeshData>(ownerObject.GetComponent<UIPrefabOwner>(), image);
            CheckHolderDiagnostics<UIMeshDataX>(ownerObject.GetComponent<UIPrefabOwner>(), image);
        }
        finally
        {
            imageObject.transform.SetParent(null, false);
            Object.DestroyImmediate(ownerObject);
            BuildMeshDiagnostics.Enabled = oldEnabled;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = oldQuad;
            UIImage.EnableSimpleMeshFastPath = oldFast;
        }
    }

    private static void CheckHolderDiagnostics<T>(UIPrefabOwner owner, UIImage image) where T : class, IUIData, new()
    {
        var holder = new DataPrefabHolder<T>();
        holder.SetTarget(owner);
        var draws = new IUIDrawTarget[] { image, image };
        try
        {
            BuildMeshDiagnostics.Enabled = true;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = true;
            UIImage.EnableSimpleMeshFastPath = false;
            holder.BuildMesh(draws);
            var expectedVertices = new System.Collections.Generic.List<Vector3>();
            var expectedUvs = new System.Collections.Generic.List<Vector4>();
            var expectedColors = new System.Collections.Generic.List<Color32>();
            var expectedIndices = new System.Collections.Generic.List<int>();
            holder.Fill(expectedVertices, expectedUvs, expectedColors, expectedIndices, Vector3.zero);

            UIImage.EnableSimpleMeshFastPath = true;
            holder.BuildMesh(draws);
            Assert.AreEqual(2, holder.LastBuildMeshTimings.SimpleQuadWrite.RecordedWrites);
            Assert.AreEqual(1, image.LastSimpleQuadWriteTimings.RecordedWrites);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector4>();
            var colors = new System.Collections.Generic.List<Color32>();
            var indices = new System.Collections.Generic.List<int>();
            holder.Fill(vertices, uvs, colors, indices, Vector3.zero);
            Assert.AreEqual(expectedVertices.Count, vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
                Assert.LessOrEqual(Vector3.Distance(expectedVertices[i], vertices[i]), 1e-4f);
            CollectionAssert.AreEqual(expectedUvs, uvs);
            CollectionAssert.AreEqual(expectedColors, colors);
            CollectionAssert.AreEqual(expectedIndices, indices);

            UIImage.EnableSimpleMeshFastPath = false;
            holder.BuildMesh(draws);
            Assert.AreEqual(default(SimpleQuadWriteTimings), holder.LastBuildMeshTimings.SimpleQuadWrite);
            Assert.AreEqual(default(SimpleQuadWriteTimings), image.LastSimpleQuadWriteTimings);

            UIImage.EnableSimpleMeshFastPath = true;
            holder.BuildMesh(draws);
            BuildMeshDiagnostics.Enabled = false;
            holder.BuildMesh(draws);
            Assert.AreEqual(default(BuildMeshStageTimings), holder.LastBuildMeshTimings);
            Assert.AreEqual(default(BuildMeshStageTimings), image.LastGenerationStageTimings);
            Assert.AreEqual(default(SimpleQuadWriteTimings), image.LastSimpleQuadWriteTimings);
        }
        finally
        {
            foreach (var mesh in holder.UIMeshDatas) mesh?.Dispose();
        }
    }

    private GameObject imageObject;
    private Texture2D texture;
    private Sprite sprite;

    [SetUp]
    public void SetUp()
    {
        imageObject = new GameObject("UIImage test");
        imageObject.AddComponent<RectTransform>().sizeDelta = new Vector2(32, 24);
        texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 1f);
    }

    [TearDown]
    public void TearDown()
    {
        UIImage.EnableSimpleMeshFastPath = true;
        Object.DestroyImmediate(sprite);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(imageObject);
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_001.md
    [Test]
    [Category("UT_UIIMAGE_001")]
    public void SimpleImage_FastPathMatchesUguiFallback()
    {
        var image = imageObject.AddComponent<UIImage>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = new Color32(10, 20, 30, 40);
        var fast = new UIMeshData { TextureIndex = 2 };
        var fallback = new UIMeshData { TextureIndex = 2 };

        UIImage.EnableSimpleMeshFastPath = true;
        image.DoGenerate(fast);
        Assert.IsTrue(image.LastGenerationUsedSimpleMeshFastPath);

        UIImage.EnableSimpleMeshFastPath = false;
        image.DoGenerate(fallback);
        Assert.IsFalse(image.LastGenerationUsedSimpleMeshFastPath);

        Assert.AreEqual(fallback.mesh.VertexCount, fast.mesh.VertexCount);
        Assert.AreEqual(fallback.mesh.IndicesCount, fast.mesh.IndicesCount);
        for (int i = 0; i < fast.mesh.VertexCount; i++)
        {
            Assert.That(fast.vertList[i].x, Is.EqualTo(fallback.vertList[i].x).Within(1e-4f));
            Assert.That(fast.vertList[i].y, Is.EqualTo(fallback.vertList[i].y).Within(1e-4f));
            Assert.AreEqual(fallback.uvs[i], fast.uvs[i]);
            Assert.AreEqual(fallback.colors[i], fast.colors[i]);
        }
        CollectionAssert.AreEqual(fallback.triangles, fast.triangles);
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_002.md
    [Test]
    [Category("UT_UIIMAGE_002")]
    public void FilledImage_UsesUguiFallback()
    {
        var image = imageObject.AddComponent<UIImage>();
        image.sprite = sprite;
        image.type = Image.Type.Filled;
        var data = new UIMeshData();

        image.DoGenerate(data);

        Assert.IsFalse(image.LastGenerationUsedSimpleMeshFastPath);
        Assert.Greater(data.mesh.VertexCount, 0);
        Assert.Greater(data.mesh.IndicesCount, 0);
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_003.md
    [Test]
    [Category("UT_UIIMAGE_003")]
    public void UnsupportedImageModes_UseUguiFallback()
    {
        var image = imageObject.AddComponent<UIImage>();
        image.sprite = sprite;
        var data = new UIMeshData();

        image.type = Image.Type.Sliced;
        image.DoGenerate(data);
        Assert.IsFalse(image.LastGenerationUsedSimpleMeshFastPath);

        image.type = Image.Type.Tiled;
        image.DoGenerate(data);
        Assert.IsFalse(image.LastGenerationUsedSimpleMeshFastPath);

        image.type = Image.Type.Simple;
        image.sprite = null;
        image.DoGenerate(data);
        Assert.IsTrue(image.LastGenerationUsedSimpleMeshFastPath);
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_005.md
    [Test]
    [Category("UT_UIIMAGE_005")]
    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_005.md
    public void NullSpriteSimpleImage_FastPathMatchesUguiFallback()
    {
        var image = imageObject.AddComponent<UIImage>();
        image.type = Image.Type.Simple;
        image.color = new Color32(10, 20, 30, 40);
        var fast = new UIMeshData { TextureIndex = 2 };
        var fallback = new UIMeshData { TextureIndex = 2 };

        UIImage.EnableSimpleMeshFastPath = true;
        image.DoGenerate(fast);
        Assert.IsTrue(image.LastGenerationUsedSimpleMeshFastPath);

        UIImage.EnableSimpleMeshFastPath = false;
        image.DoGenerate(fallback);
        Assert.IsFalse(image.LastGenerationUsedSimpleMeshFastPath);

        Assert.AreEqual(fallback.mesh.VertexCount, fast.mesh.VertexCount);
        Assert.AreEqual(fallback.mesh.IndicesCount, fast.mesh.IndicesCount);
        for (int i = 0; i < fast.mesh.VertexCount; i++)
        {
            Assert.That(fast.vertList[i].x, Is.EqualTo(fallback.vertList[i].x).Within(1e-4f));
            Assert.That(fast.vertList[i].y, Is.EqualTo(fallback.vertList[i].y).Within(1e-4f));
            Assert.AreEqual(fallback.uvs[i], fast.uvs[i]);
            Assert.AreEqual(fallback.colors[i], fast.colors[i]);
        }
        CollectionAssert.AreEqual(fallback.triangles, fast.triangles);
        Assert.AreEqual(-1, fast.TextureIndex);
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_006.md
    [Test]
    [Category("UT_UIIMAGE_006")]
    public void NullSpriteCache_TransitionsToRegisteredSprite()
    {
        var image = imageObject.AddComponent<UIImage>();
        image.type = Image.Type.Simple;
        var first = new UIMeshData();
        var second = new UIMeshData();

        var ownerObject = new GameObject("UIImage texture owner");
        var owner = ownerObject.AddComponent<UIPrefabOwner>();
        owner.targets = new System.Collections.Generic.List<Transform> { image.transform };
        var recorder = (ITextureRecorder)UIPrefabManager.Instance;
        var registration = new UIPrefabRegistration(owner, recorder);
        var imageKey = image.GetInstanceID();
        try
        {
            image.DoGenerate(first);
            image.DoGenerate(second);

            Assert.AreEqual(-1, first.TextureIndex);
            Assert.AreEqual(-1, second.TextureIndex);
            Assert.AreEqual(first.mesh.VertexCount, second.mesh.VertexCount);
            Assert.AreEqual(first.mesh.IndicesCount, second.mesh.IndicesCount);
            CollectionAssert.AreEqual(first.triangles, second.triangles);

            image.sprite = sprite;
            recorder.OnTextureRegister(imageKey, texture);
            image.DoGenerate(second);

            Assert.GreaterOrEqual(second.TextureIndex, 1);
            Assert.Greater(second.mesh.VertexCount, 0);
            Assert.Greater(second.mesh.IndicesCount, 0);
        }
        finally
        {
            recorder.OnTextureUnRegister(imageKey);
            Object.DestroyImmediate(ownerObject);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UIIMAGE_004.md
    [Test]
    [Category("UT_UIIMAGE_004")]
    public void SimpleImage_ParentTransformMatchesUguiFallback()
    {
        var root = new GameObject("UIImage root");
        root.transform.position = new Vector3(4, -3, 0);
        root.transform.rotation = Quaternion.Euler(0, 0, 15);
        imageObject.transform.SetParent(root.transform, false);
        imageObject.transform.localPosition = new Vector3(2, 5, 0);

        var image = imageObject.AddComponent<UIImage>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        var fast = new UIMeshData();
        var fallback = new UIMeshData();

        UIImage.EnableSimpleMeshFastPath = true;
        image.DoGenerate(fast, root.transform);
        UIImage.EnableSimpleMeshFastPath = false;
        image.DoGenerate(fallback, root.transform);

        for (int i = 0; i < fast.mesh.VertexCount; i++)
        {
            Assert.That(fast.vertList[i].x, Is.EqualTo(fallback.vertList[i].x).Within(1e-4f));
            Assert.That(fast.vertList[i].y, Is.EqualTo(fallback.vertList[i].y).Within(1e-4f));
            Assert.That(fast.vertList[i].z, Is.EqualTo(fallback.vertList[i].z).Within(1e-4f));
        }

        Object.DestroyImmediate(root);
    }
}
