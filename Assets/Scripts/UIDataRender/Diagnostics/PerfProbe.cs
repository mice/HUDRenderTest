using System;
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

        public void Record(float durationMs, int drawCalls = 1)
        {
            _durations[_writePos]      = durationMs;
            _drawCallCounts[_writePos] = drawCalls;
            _writePos = (_writePos + 1) % _durations.Length;
            if (_count < _durations.Length) _count++;
        }

        /// <summary>
        /// Writes aggregated stats to a CSV file in Application.persistentDataPath.
        /// Returns the full path of the written file.
        /// </summary>
        public string Flush(string deviceTag)
        {
            string dir  = Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            string ts   = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string path = Path.Combine(dir, $"perf_{deviceTag}_{ts}.csv");
            using (var w = new StreamWriter(path))
            {
                w.WriteLine("metric,avg,max");
                w.WriteLine(FormattableString.Invariant($"fill_ms,{AvgMs:F3},{MaxMs:F3}"));
                w.WriteLine(FormattableString.Invariant($"draw_calls,{AvgDrawCalls:F3},{(float)MaxDrawCalls():F3}"));
            }
            return path;
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

        private int MaxDrawCalls()
        {
            int max = int.MinValue;
            for (int i = 0; i < _count; i++)
                if (_drawCallCounts[i] > max) max = _drawCallCounts[i];
            return max;
        }
    }
}
