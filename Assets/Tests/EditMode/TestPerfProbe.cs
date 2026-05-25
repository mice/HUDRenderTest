using System.IO;
using NUnit.Framework;
using UIData;

/// <summary>
/// TC-PP-01: Sliding window avg/max are computed correctly.
/// TC-PP-02: Flush writes a well-formed CSV to disk.
/// </summary>
public class TestPerfProbe
{
    // TC-PP-01
    [Test]
    public void Window_AvgMaxCorrect()
    {
        var probe = new PerfProbe(windowSize: 5);

        float[] samples = { 1f, 2f, 3f, 4f, 5f };
        foreach (var s in samples)
            probe.Record(s);

        float expectedAvg = (1f + 2f + 3f + 4f + 5f) / 5f;
        float expectedMax = 5f;

        Assert.That(probe.AvgMs, Is.EqualTo(expectedAvg).Within(1e-4f), "AvgMs should equal Σ/N");
        Assert.That(probe.MaxMs, Is.EqualTo(expectedMax).Within(1e-4f), "MaxMs should equal max(X)");
        Assert.AreEqual(5, probe.Count, "Count should equal number of recorded samples");
    }

    // TC-PP-01 extended: wrap-around in circular buffer
    [Test]
    public void Window_CircularBuffer_WrapsCorrectly()
    {
        var probe = new PerfProbe(windowSize: 3);
        probe.Record(10f);
        probe.Record(20f);
        probe.Record(30f);
        // Buffer now full: [10,20,30]
        probe.Record(5f);
        // Wraps: oldest (10) replaced by 5 → [5,20,30]
        Assert.That(probe.MaxMs, Is.EqualTo(30f).Within(1e-4f));
        Assert.That(probe.AvgMs, Is.EqualTo((5f + 20f + 30f) / 3f).Within(1e-4f));
    }

    // TC-PP-02
    [Test]
    public void Flush_WritesCsv()
    {
        var probe = new PerfProbe(windowSize: 4);
        probe.Record(1.0f, drawCalls: 2);
        probe.Record(3.0f, drawCalls: 4);

        string path = probe.Flush("test");

        Assert.IsTrue(File.Exists(path), "CSV file must exist after Flush");

        string[] lines = File.ReadAllLines(path);
        Assert.GreaterOrEqual(lines.Length, 2, "CSV must have at least a header row and one data row");
        Assert.AreEqual("metric,avg,max", lines[0], "first line must be the CSV header");

        // Subsequent rows must contain metric name and two numeric fields
        for (int i = 1; i < lines.Length; i++)
        {
            string[] parts = lines[i].Split(',');
            Assert.AreEqual(3, parts.Length, $"row {i} must have exactly 3 comma-separated columns");
            Assert.IsTrue(float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _),
                $"row {i} column 2 (avg) must be a valid float");
            Assert.IsTrue(float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _),
                $"row {i} column 3 (max) must be a valid float");
        }

        // TC-PP-02 extended: Flush on empty probe must not produce sentinel values
        [Test]
        public void Flush_EmptyProbe_WritesZeroes()
        {
            var probe = new PerfProbe(windowSize: 4);
            // No Record calls — buffer is empty
            string path = probe.Flush("empty");

            Assert.IsTrue(File.Exists(path));
            string[] lines = File.ReadAllLines(path);
            // draw_calls row: avg and max must both be 0, not int.MinValue
            string drawRow = System.Array.Find(lines, l => l.StartsWith("draw_calls"));
            Assert.IsNotNull(drawRow, "draw_calls row must be present");
            string[] parts = drawRow.Split(',');
            float max = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(max, Is.EqualTo(0f), "MaxDrawCalls must be 0 for empty probe, not int.MinValue");

            File.Delete(path);
        }
}
