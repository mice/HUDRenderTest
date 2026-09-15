using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace UIData
{
    /// <summary>
    /// Collects per-frame timing and draw-call samples in a circular buffer.
    /// Provides avg/max aggregates and can flush results to a CSV file.
    /// </summary>
    public class PerfProbe
    {
        public const int DefaultWindowSize = 10800;
        private const string DefaultTag = "probe";

        public static readonly ProfilerMarker FillMarker     = new ProfilerMarker("UIData.Fill");
        public static readonly ProfilerMarker MergeJobMarker = new ProfilerMarker("UIData.MergeJob");
        public static readonly ProfilerMarker DrawMarker     = new ProfilerMarker("UIData.Draw");
        private readonly float[] _durations;
        private readonly int[]   _drawCallCounts;
        private int _writePos;
        private int _count;

        public PerfProbe(int windowSize = DefaultWindowSize)
        {
            if (windowSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowSize), "windowSize must be > 0");
            _durations      = new float[windowSize];
            _drawCallCounts = new int[windowSize];
        }

        public int   Count        => _count;
        public float AvgMs        => _count == 0 ? 0f : SumDurations() / _count;
        public float MaxMs        => _count == 0 ? 0f : MaxDuration();
        public float AvgDrawCalls => _count == 0 ? 0f : (float)SumDrawCalls() / _count;
        public int   MaxDrawCalls => _count == 0 ? 0  : MaxDrawCallsCore();

        public void Record(float durationMs, int drawCalls = 1)
        {
            _durations[_writePos]      = durationMs;
            _drawCallCounts[_writePos] = drawCalls;
            _writePos = (_writePos + 1) % _durations.Length;
            if (_count < _durations.Length) _count++;
        }

        /// <summary>
        /// Writes aggregated stats to a CSV file in a project-local temporary folder when
        /// running in the Unity Editor, otherwise falls back to Application.persistentDataPath.
        /// Returns the full path of the written file.
        /// </summary>
        public string Flush(string deviceTag, IReadOnlyDictionary<string, string> context = null)
        {
            string dir  = GetOutputDirectory();
            Directory.CreateDirectory(dir);
            string ts   = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string tag  = SanitizeTag(deviceTag);
            string path = Path.Combine(dir, $"perf_{tag}_{ts}.csv");
            using (var w = new StreamWriter(path))
            {
                w.WriteLine("metric,avg,max");
                w.WriteLine(FormattableString.Invariant($"fill_ms,{AvgMs:F3},{MaxMs:F3}"));
                w.WriteLine(FormattableString.Invariant($"draw_calls,{AvgDrawCalls:F3},{(float)MaxDrawCalls:F3}"));

                if (context != null && context.Count > 0)
                {
                    w.WriteLine();
                    w.WriteLine("context,value");
                    foreach (var pair in context)
                        w.WriteLine($"{EscapeCsv(pair.Key)},{EscapeCsv(pair.Value)}");
                }
            }
            return path;
        }

        public static string GetOutputDirectory()
        {
            if (Application.isEditor)
            {
                string dataPath = Application.dataPath;
                if (!string.IsNullOrEmpty(dataPath))
                {
                    var projectDir = Directory.GetParent(dataPath);
                    if (projectDir != null)
                        return Path.GetFullPath(Path.Combine(projectDir.FullName, "Logs", "PerfProbe"));
                }
            }

            return Application.persistentDataPath;
        }

        public static string SanitizeTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DefaultTag;

            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]) || Array.IndexOf(invalidChars, chars[i]) >= 0)
                {
                    chars[i] = '-';
                    continue;
                }

                if (chars[i] == Path.DirectorySeparatorChar || chars[i] == Path.AltDirectorySeparatorChar)
                    chars[i] = '-';
            }

            string sanitized = new string(chars).Trim('-', '_');
            return string.IsNullOrEmpty(sanitized) ? DefaultTag : sanitized;
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needsQuotes)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private float SumDurations()
        {
            float sum = 0f;
            for (int i = 0; i < _count; i++) sum += _durations[i];
            return sum;
        }

        private float MaxDuration()
        {
            float max = float.MinValue;
            for (int i = 0; i < _count; i++)
                if (_durations[i] > max) max = _durations[i];
            return max;
        }

        private int SumDrawCalls()
        {
            int sum = 0;
            for (int i = 0; i < _count; i++) sum += _drawCallCounts[i];
            return sum;
        }

        private int MaxDrawCallsCore()
        {
            int max = int.MinValue;
            for (int i = 0; i < _count; i++)
                if (_drawCallCounts[i] > max) max = _drawCallCounts[i];
            return max;
        }
    }
}
