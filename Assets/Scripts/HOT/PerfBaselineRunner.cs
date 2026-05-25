using System;
using System.IO;
using UIData;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-level helper that flushes a probe after a warmup + sample window.
/// When no source probe is supplied, it falls back to recording frame time locally.
/// </summary>
public class PerfBaselineRunner : MonoBehaviour
{
    public MonoBehaviour probeSourceBehaviour;
    public Text outputText;
    public string deviceTag = "baseline";
    public int warmupFrames = 60;
    public int sampleFrames = 600;
    public bool autoStart = true;
    public bool autoOpenFolderAfterFlush;
    public bool quitAfterFlush;
    public bool recordFrameTimeWhenSourceMissing = true;

    [Button(nameof(StartSampling))]
    public string _startSampling;
    [Button(nameof(FlushNow))]
    public string _flushNow;
    [Button(nameof(OpenCsvFolder))]
    public string _openCsvFolder;

    private IPerfProbeSource probeSource;
    private PerfProbe fallbackProbe;
    private int elapsedFrames;
    private bool isRunning;
    private bool hasFlushed;

    public string LastCsvPath { get; private set; }
    public PerfProbe Probe => probeSource != null ? probeSource.Probe : fallbackProbe;

    private void Start()
    {
        if (autoStart)
            StartSampling();
    }

    public void StartSampling()
    {
        probeSource = ResolveProbeSource();
        if (probeSource == null && recordFrameTimeWhenSourceMissing)
            fallbackProbe = new PerfProbe(Mathf.Max(sampleFrames, 1));
        else
            fallbackProbe = null;

        elapsedFrames = 0;
        isRunning = true;
        hasFlushed = false;
        LastCsvPath = null;
        UpdateOutputText("Sampling...");
    }

    public void FlushNow()
    {
        var probe = Probe;
        if (probe == null)
        {
            UpdateOutputText("No probe");
            return;
        }

        LastCsvPath = probe.Flush(deviceTag);
        hasFlushed = true;
        isRunning = false;
        UpdateOutputText(LastCsvPath);

        if (autoOpenFolderAfterFlush)
            OpenCsvFolder();

        if (quitAfterFlush)
            Application.Quit();
    }

    public void OpenCsvFolder()
    {
        string directory = string.IsNullOrEmpty(LastCsvPath)
            ? Application.persistentDataPath
            : Path.GetDirectoryName(LastCsvPath);

        if (string.IsNullOrEmpty(directory))
            directory = Application.persistentDataPath;

        Application.OpenURL(new Uri(directory).AbsoluteUri);
    }

    private void Update()
    {
        if (!isRunning || hasFlushed)
            return;

        if (elapsedFrames >= warmupFrames && probeSource == null && fallbackProbe != null)
            fallbackProbe.Record(Time.unscaledDeltaTime * 1000f, 0);

        elapsedFrames++;
        if (elapsedFrames >= warmupFrames + sampleFrames)
            FlushNow();
    }

    private IPerfProbeSource ResolveProbeSource()
    {
        if (probeSourceBehaviour is IPerfProbeSource directSource)
            return directSource;

        if (probeSourceBehaviour != null)
            return null;

        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPerfProbeSource source)
                return source;
        }
        return null;
    }

    private void UpdateOutputText(string value)
    {
        if (outputText != null)
            outputText.text = value;
    }
}
