using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Owns one ordered Built-in HUD pass. Refresh after publication and before camera rendering.</summary>
public sealed class HolderBucketCameraPass : IDisposable
{
    private Camera _camera;
    private IHolderDrawSource _source;
    private CommandBuffer _commands;
    private readonly CameraEvent _cameraEvent;
    private Matrix4x4 _matrix;
    private int _shaderPass;
    private long _recordedRevision = -1;
    private bool _hasRefresh;
    public int LastDrawCount { get; private set; }

    public HolderBucketCameraPass(Camera camera, IHolderDrawSource source,
        CameraEvent cameraEvent = CameraEvent.AfterForwardAlpha)
    {
        if (camera == null) throw new ArgumentNullException(nameof(camera));
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (GraphicsSettings.currentRenderPipeline != null)
            throw new NotSupportedException("HolderBucketCameraPass requires the Built-in render pipeline.");
        _camera = camera;
        _source = source;
        _cameraEvent = cameraEvent;
        _commands = new CommandBuffer { name = "UIData ordered holder buckets" };
        _camera.AddCommandBuffer(_cameraEvent, _commands);
        Camera.onPreCull += OnPreCull;
    }

    public int Refresh(Matrix4x4 matrix, int shaderPass = 0)
    {
        if (_commands == null) throw new ObjectDisposedException(nameof(HolderBucketCameraPass));
        _commands.Clear();
        LastDrawCount = 0;
        try
        {
            LastDrawCount = _source.RecordDraws(_commands, matrix, shaderPass);
            _matrix = matrix; _shaderPass = shaderPass;
            _recordedRevision = _source.DrawRevision;
            _hasRefresh = true;
        }
        catch { _commands.Clear(); throw; }
        return LastDrawCount;
    }

    private void OnPreCull(Camera camera)
    {
        if (camera != _camera) return;
        // A resource mapping can change after Refresh. Never execute stale resource bindings.
        if (!_source.CanDraw) { _commands.Clear(); LastDrawCount = 0; }
        else if (_hasRefresh && _source.DrawRevision != _recordedRevision)
            Refresh(_matrix, _shaderPass);
    }

    public void Dispose()
    {
        if (_commands == null) return;
        Camera.onPreCull -= OnPreCull;
        if (_camera != null) _camera.RemoveCommandBuffer(_cameraEvent, _commands);
        _commands.Release();
        _commands = null;
        _camera = null;
        _source = null;
        LastDrawCount = 0;
    }
}
