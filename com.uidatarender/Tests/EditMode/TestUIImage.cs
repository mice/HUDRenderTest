using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestUIImage
{
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
