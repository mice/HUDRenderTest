using System;
using UnityEngine;

/// <summary>Interactive visual-only playback. Both adapters receive one clock; no benchmark report is produced.</summary>
public sealed class DamageTextComparisonPreview : IDisposable
{
    private Camera _original, _leftCamera, _rightCamera;
    private bool _originalEnabled;
    private DamageTextClip _clip;
    private double _time;
    public DamageTextPlayback Left { get; private set; }
    public DamageTextPlayback Right { get; private set; }
    public bool Paused { get; private set; } = true;
    public bool Formal => false;
    public float PlaybackTime => (float)_time;
    public void Open(Camera camera, DamageTextClip clip, DamageTextConfiguration configuration, DamageTextGlyphAtlas atlas,
        DamageTextStyles styles, Shader meshShader, Shader instanceShader, bool sideBySide,
        DamageTextGeometry geometry = DamageTextGeometry.PersistentMesh, DamageTextPositionFormat format = DamageTextPositionFormat.Float32)
    {
        Dispose();
        if (camera == null || clip == null) throw new ArgumentNullException();
        _original = camera; _originalEnabled = camera.enabled; _clip = clip; _time = 0; Paused = true;
        try
        {
            _leftCamera = CloneCamera(camera, sideBySide ? new Rect(0, 0, .5f, 1) : camera.rect, "Damage comparison visual left");
            Left = new DamageTextPlayback(geometry: sideBySide ? DamageTextGeometry.PersistentMesh : geometry,
                positionFormat: sideBySide || geometry == DamageTextGeometry.PersistentMesh ? format : DamageTextPositionFormat.Float32);
            Left.Prepare(clip, configuration, atlas, styles, !sideBySide && geometry == DamageTextGeometry.InstancedQuad ? instanceShader : meshShader, _leftCamera);
            if (sideBySide)
            {
                _rightCamera = CloneCamera(camera, new Rect(.5f, 0, .5f, 1), "Damage comparison visual right");
                _rightCamera.depth = _leftCamera.depth + 1;
                Right = new DamageTextPlayback(geometry: DamageTextGeometry.InstancedQuad);
                Right.Prepare(clip, configuration, atlas, styles, instanceShader, _rightCamera);
            }
            camera.enabled = false;
        }
        catch { Dispose(); throw; }
    }
    private static Camera CloneCamera(Camera source, Rect rect, string name)
    {
        var camera = new GameObject(name, typeof(Camera)).GetComponent<Camera>(); camera.CopyFrom(source);
        camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        camera.rect = rect; camera.enabled = true; return camera;
    }
    public void SetPaused(bool paused) { Paused = paused; }
    public void Advance(double elapsedSeconds)
    {
        if (Paused || Left == null) return;
        Step(elapsedSeconds);
    }
    public void Step(double elapsedSeconds = 1.0 / 60)
    {
        if (Left == null || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        _time = Math.Min(_clip.Duration, _time + elapsedSeconds); Left.AdvanceTo(_time); Right?.AdvanceTo(_time);
        if (_time >= _clip.Duration) Paused = true;
    }
    public void Seek(float time)
    {
        if (Left == null) return;
        Left.Seek(time); Right?.Seek(time); _time = time; Paused = true;
    }
    public void DrawControls()
    {
        GUILayout.Label("Visual inspection · left B / right A when split · timing excluded");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Paused ? "Play" : "Pause")) Paused = !Paused;
        if (GUILayout.Button("Step")) { Paused = true; Step(); }
        if (GUILayout.Button("Restart")) Seek(0);
        GUILayout.EndHorizontal();
        float selected = GUILayout.HorizontalSlider(PlaybackTime, 0, _clip.Duration);
        if (!Mathf.Approximately(selected, PlaybackTime)) Seek(selected);
        GUILayout.Label(PlaybackTime.ToString("F3") + "s");
    }
    public void Dispose()
    {
        Left?.Dispose(); Right?.Dispose(); Left = Right = null;
        DestroyCamera(_leftCamera); DestroyCamera(_rightCamera); _leftCamera = _rightCamera = null;
        if (_original != null) _original.enabled = _originalEnabled;
        _original = null; _clip = null;
    }
    private static void DestroyCamera(Camera camera)
    {
        if (camera == null) return;
        camera.enabled = false;
        if (Application.isPlaying) UnityEngine.Object.Destroy(camera.gameObject); else UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }
}
