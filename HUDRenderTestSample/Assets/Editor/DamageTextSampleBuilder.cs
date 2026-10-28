using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates only new Sample-owned assets. Never overwrites an existing scene or atlas.</summary>
public static class DamageTextSampleBuilder
{
    public const string ScenePath = "Assets/Scenes/Scene_DamageTextPlayback.unity";
    public const string ResourcePath = "Assets/DamageText";

    [MenuItem("Tools/HUD Damage Text/Create playback Sample")]
    public static void CreateSample()
    {
        if (File.Exists(ScenePath) || Directory.Exists(ResourcePath))
            throw new InvalidOperationException("Damage-text Sample assets already exist; open the existing scene instead of overwriting it.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(ResourcePath);
        var texture = new Texture2D(256, 32, TextureFormat.RGBA32, false);
        try
        {
            var pixels = new Color32[256 * 32];
            // Original 5x7 binary digit patterns, enlarged 3x. No external font or dynamic atlas dependency.
            string[] patterns =
            {
                "01110100011001110101110011000101110", "00100011000010000100001000010001110",
                "01110100010000100010001000100011111", "11110000010000101110000010000111110",
                "00010001100101010010111110001000010", "11111100001000011110000010000111110",
                "01110100001000011110100011000101110", "11111000010001000100010000100001000",
                "01110100011000101110100011000101110", "01110100011000101111000010000101110"
            };
            var glyphs = new DamageTextGlyph[10];
            for (int d = 0; d < 10; d++)
            {
                for (int row = 0; row < 7; row++) for (int col = 0; col < 5; col++)
                    if (patterns[d][row * 5 + col] == '1')
                        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                            pixels[(5 + (6 - row) * 3 + y) * 256 + d * 24 + 4 + col * 3 + x] = new Color32(255, 255, 255, 255);
                glyphs[d] = new DamageTextGlyph
                {
                    Available = true, Size = new Vector2(15, 21), Advance = 17,
                    Uv = new Rect((d * 24 + 4) / 256f, 5 / 32f, 15 / 256f, 21 / 32f)
                };
            }
            texture.SetPixels32(pixels); texture.Apply();
            byte[] png = texture.EncodeToPNG(); string texturePath = ResourcePath + "/Digits5x7.png";
            File.WriteAllBytes(texturePath, png); AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            string textureHash;
            using (var hash = SHA256.Create()) textureHash = BitConverter.ToString(hash.ComputeHash(png)).Replace("-", "");
            var atlas = ScriptableObject.CreateInstance<DamageTextGlyphAtlas>();
            atlas.Initialize(AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath), 24, glyphs, "sample-5x7-v1", textureHash);
            AssetDatabase.CreateAsset(atlas, ResourcePath + "/Digits.asset");
            var styles = ScriptableObject.CreateInstance<DamageTextStyles>();
            AssetDatabase.CreateAsset(styles, ResourcePath + "/Styles.asset");
            var clips = new[] { CreateBoundaries(), CreateContinuous(), CreateBurst() };
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Damage text camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .04f, .065f);
            var runner = new GameObject("Damage text playback", typeof(DamageTextPlaybackRunner)).GetComponent<DamageTextPlaybackRunner>();
            runner.outputCamera = camera; runner.atlas = atlas; runner.styles = styles; runner.clips = clips;
            runner.shader = Shader.Find("UIData/DamageText");
            if (runner.shader == null) throw new InvalidOperationException("Damage-text Shader was not imported.");
            AssetDatabase.SaveAssets(); runner.RefreshEditorSourceMetadata();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save damage-text Sample.");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static DamageTextClipAsset SaveClip(string name, DamageTextRequest[] events, float duration)
    {
        var clip = ScriptableObject.CreateInstance<DamageTextClipAsset>();
        clip.Events = events; clip.Duration = duration;
        clip.CameraKeys = new[]
        {
            new DamageTextCameraKey { Time = 0, Position = new Vector3(0, 0, -10), Rotation = Quaternion.identity, FieldOfView = 60, OrthographicSize = 5 },
            new DamageTextCameraKey { Time = duration, Position = new Vector3(.5f, .25f, -10), Rotation = Quaternion.identity, FieldOfView = 60, OrthographicSize = 5 }
        };
        clip.Freeze(); AssetDatabase.CreateAsset(clip, ResourcePath + "/" + name + ".asset"); return clip;
    }
    private static DamageTextClipAsset CreateBoundaries()
    {
        var events = new DamageTextRequest[48]; uint[] values = { 10, 99, 100, 99999999 };
        for (int i = 0; i < events.Length; i++) events[i] = new DamageTextRequest
        {
            EventId = (uint)i + 1, Amount = values[i % 4], Kind = (DamageTextKind)(i / 4 % 3),
            WorldPosition = new Vector3((i % 4 - 1.5f) * 2.5f, (i / 4 % 3 - 1) * 1.6f - .5f, 0),
            SpawnTime = i / 12 * 1.5f, RandomSeed = (uint)(i * 71 + 3)
        };
        return SaveClip("Boundaries", events, 6);
    }
    private static DamageTextClipAsset CreateContinuous()
    {
        var events = new DamageTextRequest[384]; uint seed = 12345;
        for (int i = 0; i < events.Length; i++)
        {
            unchecked { seed = seed * 1664525 + 1013904223; }
            events[i] = new DamageTextRequest
            {
                EventId = (uint)i + 1, Amount = 10 + seed % 99999990, Kind = (DamageTextKind)(i % 3),
                WorldPosition = new Vector3((seed & 1023) / 1023f * 10 - 5, ((seed >> 10) & 1023) / 1023f * 5 - 3, 0),
                SpawnTime = i * .02f, RandomSeed = seed
            };
        }
        return SaveClip("Continuous", events, 9);
    }
    private static DamageTextClipAsset CreateBurst()
    {
        var events = new DamageTextRequest[127 + 128 + 129 + 256]; int index = 0, group = 0;
        foreach (int count in new[] { 127, 128, 129, 256 })
        {
            for (int i = 0; i < count; i++)
            {
                events[index] = new DamageTextRequest
                {
                    EventId = (uint)index + 1, Amount = 99999999, Kind = (DamageTextKind)(i % 3),
                    WorldPosition = new Vector3((i % 16 - 7.5f) * .8f, (i / 16 - 7.5f) * .45f, 0),
                    SpawnTime = group * 1.5f, RandomSeed = (uint)index + 7
                }; index++;
            }
            group++;
        }
        return SaveClip("Bursts127_128_129_256", events, 6);
    }
    public static void StampMetadataBatch()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var runner = UnityEngine.Object.FindObjectOfType<DamageTextPlaybackRunner>();
            if (runner == null) throw new InvalidOperationException("Damage-text Sample runner is missing.");
            AssetDatabase.SaveAssets(); runner.RefreshEditorSourceMetadata(); EditorUtility.SetDirty(runner);
            EditorSceneManager.SaveScene(scene); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void BuildBatch()
    {
        try { CreateSample(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
