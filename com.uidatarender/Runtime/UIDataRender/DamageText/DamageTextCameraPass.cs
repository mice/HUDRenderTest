using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Per-camera frame uniforms precede revision-cached ordered draws.</summary>
public sealed class DamageTextCameraPass : IDisposable
{
    private static readonly int TimeId = Shader.PropertyToID("_DamageTextPlaybackTime");
    private static readonly int ViewProjectionId = Shader.PropertyToID("_DamageTextViewProjection");
    private static readonly int ViewId = Shader.PropertyToID("_DamageTextView");
    private static readonly int ViewportId = Shader.PropertyToID("_DamageTextViewport");
    private const CameraEvent Event = CameraEvent.AfterForwardAlpha;
    private Camera _camera;
    private DamageTextRenderer _renderer;
    private CommandBuffer _parameters;
    private HolderBucketCameraPass _draws;
    public int LastDrawCount => _draws?.LastDrawCount ?? 0;

    internal DamageTextCameraPass(Camera camera, DamageTextRenderer renderer)
    {
        if (camera == null) throw new ArgumentNullException(nameof(camera));
        if (GraphicsSettings.currentRenderPipeline != null) throw new NotSupportedException("Damage text requires Built-in rendering.");
        _camera = camera; _renderer = renderer;
        try
        {
            _parameters = new CommandBuffer { name = "Damage text frame parameters" };
            camera.AddCommandBuffer(Event, _parameters);
            Camera.onPreCull += OnPreCull;
            _draws = new HolderBucketCameraPass(camera, renderer, Event);
            Refresh();
        }
        catch { Dispose(); throw; }
    }

    public void Refresh()
    {
        if (_parameters == null) throw new ObjectDisposedException(nameof(DamageTextCameraPass));
        UpdateParameters(); _draws.Refresh(Matrix4x4.identity);
    }
    private void OnPreCull(Camera camera) { if (camera == _camera) UpdateParameters(); }
    private void UpdateParameters()
    {
        _parameters.Clear();
        var projection = _camera.projectionMatrix;
        var gpuProjection = GL.GetGPUProjectionMatrix(projection, _camera.targetTexture != null);
        _parameters.SetGlobalFloat(TimeId, _renderer.PlaybackTime);
        _parameters.SetGlobalMatrix(ViewProjectionId,
            gpuProjection * _camera.worldToCameraMatrix);
        _parameters.SetGlobalMatrix(ViewId, _camera.worldToCameraMatrix);
        _parameters.SetGlobalVector(ViewportId,
            new Vector4(Mathf.Max(1, _camera.pixelWidth), Mathf.Max(1, _camera.pixelHeight), _camera.nearClipPlane,
                Mathf.Sign(gpuProjection.m11 * projection.m11)));
        _renderer.ObserveCamera(_camera.worldToCameraMatrix, _camera.nearClipPlane);
    }
    public void Dispose()
    {
        _draws?.Dispose(); _draws = null;
        Camera.onPreCull -= OnPreCull;
        if (_parameters != null)
        {
            if (_camera != null) _camera.RemoveCommandBuffer(Event, _parameters);
            _parameters.Release(); _parameters = null;
        }
        _camera = null; _renderer = null;
    }
}
