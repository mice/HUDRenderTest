using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public interface IEightSlotPerfTarget : IPerfProbeSource
{
    bool Enable8TexSlots { get; set; }
    bool RebuildEveryFrame { get; set; }
    string CsvTag { get; set; }
    string LastCsvPath { get; }
    void ReCreate();
    void FlushProbe();
}

/// <summary>
/// Runs a local A/B comparison for the 3-slot and 8-slot scene configurations.
/// It reuses BatchMergeBatcherRender so the scene can flush comparable CSV outputs
/// before manual Profiler / RenderDoc capture.
/// </summary>
public class EightSlotPerfRunner : MonoBehaviour
{
    public BatchMergeBatcherRender target;
    public MonoBehaviour targetBehaviour;
    public Text outputText;
    public string baseTag = "8slot_compare";
    public int warmupFrames = 60;
    public int sampleFrames = 600;
    public bool autoStart = true;
    public bool forceRebuildEveryFrame = true;
    public bool restoreOriginalStateOnComplete = true;

    [Button(nameof(StartComparison))]
    public string _startComparison;
    [Button(nameof(OpenLastCsvFolder))]
    public string _openLastCsvFolder;

    private Phase phase;
    private int phaseFrame;
    private bool originalEnable8TexSlots;
    private bool originalRebuildEveryFrame;
    private string originalCsvTag;
    private IEightSlotPerfTarget activeTarget;

    public string ThreeSlotCsvPath { get; private set; }
    public string EightSlotCsvPath { get; private set; }

    private enum Phase
    {
        Idle = 0,
        Warmup3Slot = 1,
        Sample3Slot = 2,
        Warmup8Slot = 3,
        Sample8Slot = 4,
        Completed = 5
    }

    private void Start()
    {
        if (autoStart)
            StartComparison();
    }

    public void StartComparison()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Only Run In PlayMode");
            return;
        }

        activeTarget = ResolveTarget();
        if (activeTarget == null)
        {
            Debug.LogError("EightSlotPerfRunner requires a target that implements IEightSlotPerfTarget.");
            return;
        }

        originalEnable8TexSlots = activeTarget.Enable8TexSlots;
        originalRebuildEveryFrame = activeTarget.RebuildEveryFrame;
        originalCsvTag = activeTarget.CsvTag;

        ThreeSlotCsvPath = null;
        EightSlotCsvPath = null;

        BeginWarmup(false);
    }

    public void OpenLastCsvFolder()
    {
        string path = !string.IsNullOrEmpty(EightSlotCsvPath)
            ? EightSlotCsvPath
            : ThreeSlotCsvPath;

        string directory = string.IsNullOrEmpty(path)
            ? UIData.PerfProbe.GetOutputDirectory()
            : Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(directory))
            directory = UIData.PerfProbe.GetOutputDirectory();

        Application.OpenURL(new Uri(directory).AbsoluteUri);
    }

    private void Update()
    {
        switch (phase)
        {
            case Phase.Warmup3Slot:
                TickWarmup(false);
                break;
            case Phase.Sample3Slot:
                TickSample(false);
                break;
            case Phase.Warmup8Slot:
                TickWarmup(true);
                break;
            case Phase.Sample8Slot:
                TickSample(true);
                break;
        }
    }

    private void TickWarmup(bool enable8TexSlots)
    {
        phaseFrame++;
        if (phaseFrame >= Mathf.Max(warmupFrames, 0))
            BeginSample(enable8TexSlots);
    }

    private void TickSample(bool enable8TexSlots)
    {
        phaseFrame++;
        if (phaseFrame < Mathf.Max(sampleFrames, 1))
            return;

        activeTarget.FlushProbe();
        if (enable8TexSlots)
            EightSlotCsvPath = activeTarget.LastCsvPath;
        else
            ThreeSlotCsvPath = activeTarget.LastCsvPath;

        if (enable8TexSlots)
            CompleteComparison();
        else
            BeginWarmup(true);
    }

    private void BeginWarmup(bool enable8TexSlots)
    {
        ConfigureTarget(enable8TexSlots);
        phase = enable8TexSlots ? Phase.Warmup8Slot : Phase.Warmup3Slot;
        phaseFrame = 0;
        UpdateOutputText(enable8TexSlots ? "Warmup 8-slot..." : "Warmup 3-slot...");
    }

    private void BeginSample(bool enable8TexSlots)
    {
        ConfigureTarget(enable8TexSlots);
        phase = enable8TexSlots ? Phase.Sample8Slot : Phase.Sample3Slot;
        phaseFrame = 0;
        UpdateOutputText(enable8TexSlots ? "Sampling 8-slot..." : "Sampling 3-slot...");
    }

    private void CompleteComparison()
    {
        phase = Phase.Completed;

        if (restoreOriginalStateOnComplete)
        {
            activeTarget.Enable8TexSlots = originalEnable8TexSlots;
            activeTarget.RebuildEveryFrame = originalRebuildEveryFrame;
            activeTarget.CsvTag = originalCsvTag;
            activeTarget.ReCreate();
        }

        UpdateOutputText($"3-slot: {ThreeSlotCsvPath}\n8-slot: {EightSlotCsvPath}");
    }

    private void ConfigureTarget(bool enable8TexSlots)
    {
        activeTarget.Enable8TexSlots = enable8TexSlots;
        activeTarget.CsvTag = BuildTag(enable8TexSlots);
        if (forceRebuildEveryFrame)
            activeTarget.RebuildEveryFrame = true;

        activeTarget.ReCreate();
    }

    private string BuildTag(bool enable8TexSlots)
    {
        return $"{baseTag}_{(enable8TexSlots ? "8slot" : "3slot")}";
    }

    private IEightSlotPerfTarget ResolveTarget()
    {
        if (target != null)
            return target;

        return targetBehaviour as IEightSlotPerfTarget;
    }

    private void UpdateOutputText(string value)
    {
        if (outputText != null)
            outputText.text = value;
    }
}
