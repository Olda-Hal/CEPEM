using System.Buffers.Binary;
using System.Text;
using DatabaseAPI.Services;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// MEIK .tdb parsing: header conditions, the seven stored 36x36 display
/// planes, the -0.15 conductivity correction and per-slice histograms.
/// </summary>
public class TdbAnalysisServiceTests
{
    private const int FileSize = 456909;
    private const int PlanesBytes = 7 * 36 * 36 * 4;

    private static byte[] BuildMeasurement(string conditions = "left lying")
    {
        var data = new byte[FileSize];
        Encoding.ASCII.GetBytes("TOMODB v2.0").CopyTo(data, 0);

        var conditionBytes = Encoding.ASCII.GetBytes(conditions);
        conditionBytes.CopyTo(data, 0x0081);

        // 100.0 / 200.0 in the noise field (x100) is irrelevant here but
        // keeps the header realistic.
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x0100, 2), 150);

        var tail = data.Length - PlanesBytes;
        for (var layer = 0; layer < 7; layer++)
        {
            for (var cell = 0; cell < 36 * 36; cell++)
            {
                var value = 0.1f + layer * 0.05f + cell / 100000f;
                BinaryPrimitives.WriteSingleLittleEndian(
                    data.AsSpan(tail + (layer * 36 * 36 + cell) * 4, 4), value);
            }
        }

        return data;
    }

    [Fact]
    public void IsTdb_RecognisesMagicAndExtension()
    {
        var service = new TdbAnalysisService();
        var data = BuildMeasurement();

        Assert.True(service.IsTdb(data));
        Assert.True(service.IsTdbFileName("mereni.TDB"));
        Assert.False(service.IsTdbFileName("mereni.pdf"));
        Assert.False(service.IsTdb(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void Parse_ReadsSevenSlicesConditionsAndHistograms()
    {
        var service = new TdbAnalysisService();

        var analysis = service.Parse(BuildMeasurement("right standing"));

        Assert.Equal("Right", analysis.Side);
        Assert.Equal("Standing", analysis.Posture);
        Assert.Equal(1.5, analysis.NoiseLevel, 3);
        Assert.Equal(7, analysis.Slices.Count);
        Assert.Equal(new[] { 4, 11, 18, 25, 32, 39, 46 }, analysis.Slices.Select(s => s.DepthMm));

        var first = analysis.Slices[0];
        Assert.Equal(36, first.Width);
        Assert.Equal(36 * 36, first.Values.Length);
        Assert.Equal(-0.05, first.Values[0], 2); // 0.1 - 0.15 correction
        Assert.Equal(first.HistogramCounts.Sum(), first.Values.Count(v => v != 0f));
        Assert.Equal(33, first.HistogramEdges.Length);
        Assert.Equal(-0.30, analysis.HistogramLow, 3);
        Assert.Equal(1.10, analysis.HistogramHigh, 3);

        Assert.True(analysis.WindowHigh > analysis.WindowLow);
    }

    [Fact]
    public void Parse_RejectsNonTdbData()
    {
        var service = new TdbAnalysisService();

        Assert.Throws<InvalidOperationException>(() => service.Parse(new byte[1024]));
    }

    [Fact]
    public void Parse_WithoutDisplayTail_FallsBackToScanlineGrids()
    {
        // A short record: header only plus two populated 16x16 scanline slots.
        var data = new byte[0x0181 + 2 * 1288];
        Encoding.ASCII.GetBytes("TOMODB v2.0").CopyTo(data, 0);
        Encoding.ASCII.GetBytes("left lying").CopyTo(data, 0x0081);

        for (var slot = 0; slot < 2; slot++)
        {
            var record = 0x0181 + slot * 1288;
            for (var i = 0; i < 256; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(
                    data.AsSpan(record + (6 + i) * 4, 4), 15000f + i + slot);
            }
        }

        var service = new TdbAnalysisService();
        var analysis = service.Parse(data);

        Assert.Equal(2, analysis.Slices.Count);
        Assert.All(analysis.Slices, s => Assert.Equal(36, s.Width));
        Assert.Equal("Left", analysis.Side);
        Assert.Equal("Lying", analysis.Posture);
    }
}
