using System;
using System.IO;
using UIData;
using UnityEngine;
using UnityEngine.UI;

public interface IDynamicSwapTarget : IPerfProbeSource
{
    string CsvTag { get; set; }
    string LastCsvPath { get; }
    void CycleSprite();
    void FlushProbe();
}

/// <summary>
/// Drives repeated sprite swaps on a target and optionally flushes a CSV when the sequence completes.
/// Intended for the T-S-A-04 dynamic swap validation scene.
/// </summary>
public class DynamicSwapRunner : MonoBehaviour
{
    public BatchMergeBatcherRender target;
    public MonoBehaviour targetBehaviour;
    public Text outputText;
    public string baseTag = "dynamic_swap";
    public int warmupFrames = 30;
    public int swapCount = 10;
    public int framesBetweenSwaps = 5;
    public bool autoStart = true;
    public bool flushAfterCompletion = true;
    public bool restoreOriginalCsvTag = true;

    [Button(nameof(StartSequence))]
    public string _startSequence;
    [Button(nameof(OpenLastCsvFolder))]
    public string _openLastCsvFolder;

    private IDynamicSwapTarget activeTarget;
    private int frameCounter;
    private int swapsCompleted;
    private bool startedCycling;
    private bool isRunning;
    private string originalCsvTag;

    public string LastCsvPath { get; private set; }

    private void Start()
    {
        if (autoStart)
            StartSequence();
    }

    public void StartSequence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Only Run In PlayMode");
            return;
        }

        activeTarget = ResolveTarget();
        if (activeTarget == null)
        {
            Debug.LogError("DynamicSwapRunner requires a target that implements IDynamicSwapTarget.");
            return;
        }

        TryBindOutputText();
        originalCsvTag = activeTarget.CsvTag;
        LastCsvPath = null;
        frameCounter = 0;
        swapsCompleted = 0;
        startedCycling = warmupFrames <= 0;
        isRunning = true;
        activeTarget.CsvTag = BuildTag();

        Debug.Log($"[DynamicSwapRunner] Started: warmup={warmupFrames}, swaps={swapCount}, interval={framesBetweenSwaps}, tag={activeTarget.CsvTag}");
        UpdateOutputText(startedCycling ? "Cycling sprites..." : "Warmup before sprite cycling...");
        if (swapCount <= 0 && startedCycling)
            CompleteSequence();
    }

    public void OpenLastCsvFolder()
    {
        string directory = string.IsNullOrEmpty(LastCsvPath)
            ? PerfProbe.GetOutputDirectory()
            : Path.GetDirectoryName(LastCsvPath);

        if (string.IsNullOrEmpty(directory))
            directory = PerfProbe.GetOutputDirectory();

        Application.OpenURL(new Uri(directory).AbsoluteUri);
    }

    private void Update()
    {
        if (!isRunning)
            return;

        frameCounter++;

        if (!startedCycling)
        {
            if (frameCounter >= Mathf.Max(warmupFrames, 0))
            {
                startedCycling = true;
                frameCounter = 0;
                UpdateOutputText("Cycling sprites...");

                if (swapCount <= 0)
                    CompleteSequence();
            }

            return;
        }

        if (frameCounter < Mathf.Max(framesBetweenSwaps, 1))
            return;

        frameCounter = 0;
        activeTarget.CycleSprite();
        swapsCompleted++;

        if (swapsCompleted >= Mathf.Max(swapCount, 1))
            CompleteSequence();
    }

    private void CompleteSequence()
    {
        isRunning = false;

        if (flushAfterCompletion)
        {
            activeTarget.FlushProbe();
            LastCsvPath = activeTarget.LastCsvPath;
        }

        if (restoreOriginalCsvTag)
            activeTarget.CsvTag = originalCsvTag;

        Debug.Log($"[DynamicSwapRunner] Completed: swaps={swapsCompleted}, csv={LastCsvPath}");
        UpdateOutputText($"swaps:{swapsCompleted}\n{LastCsvPath}");
    }

    private string BuildTag()
    {
        string prefix = string.IsNullOrWhiteSpace(baseTag) ? originalCsvTag : baseTag;
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "dynamic_swap";

        return $"{prefix}_{Mathf.Max(swapCount, 0)}swaps";
    }

    private IDynamicSwapTarget ResolveTarget()
    {
        if (target != null)
            return target;

        return targetBehaviour as IDynamicSwapTarget;
    }

    private void TryBindOutputText()
    {
        if (outputText != null)
            return;

        var renderTarget = target != null ? target : targetBehaviour as BatchMergeBatcherRender;
        if (renderTarget != null)
            outputText = renderTarget.statusText;
    }

    private void UpdateOutputText(string value)
    {
        if (outputText != null)
            outputText.text = value;
    }
}
