using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace DatabaseAPI.Services;

/// <summary>
/// Reader for MEIK "TOMODB v2.0" (.tdb) measurements.
///
/// The format and the display pipeline were recovered from the original
/// MEIK.exe (see the MEIK_remake/meikpro docs):
///  * header: magic "TOMODB v2.0", conditions text in CP1251 at 0x81,
///    noise level at 0x100;
///  * the last 7*36*36 float32 values are the seven display planes
///    (4..46 mm), layer-major;
///  * the application adds the conductivity correction -0.15 and renders
///    with a 32-step palette over the global min/max of all seven planes.
///
/// This service normalises those planes for the web frontend, which does the
/// (exact, Lanczos-3) upscale and palette mapping in the browser.
/// </summary>
public interface ITdbAnalysisService
{
    bool IsTdb(byte[] data);
    bool IsTdbFileName(string? fileName);

    /// <summary>Parse a measurement; throws <see cref="InvalidOperationException"/> for non-TDB data.</summary>
    TdbAnalysis Parse(byte[] data);

    /// <summary>Serialize the analysis into the JSON stored on EvaluationCase.SlicesJson.</summary>
    string ToJson(TdbAnalysis analysis);
}

public sealed class TdbAnalysis
{
    public string Conditions { get; set; } = string.Empty;
    public string? Side { get; set; }       // "Left" / "Right"
    public string? Posture { get; set; }    // "Lying" / "Standing" / "Sitting"
    public double NoiseLevel { get; set; }
    public double WindowLow { get; set; }
    public double WindowHigh { get; set; }
    /// <summary>Fixed X-axis range for the slice histograms (MEIK panel data).</summary>
    public double HistogramLow { get; set; }
    public double HistogramHigh { get; set; }
    public List<TdbSlice> Slices { get; set; } = new();
}

public sealed class TdbSlice
{
    public int Index { get; set; }
    public int DepthMm { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public float[] Values { get; set; } = Array.Empty<float>();
    public double Min { get; set; }
    public double Max { get; set; }
    public double Mean { get; set; }
    public int[] HistogramCounts { get; set; } = Array.Empty<int>();
    public double[] HistogramEdges { get; set; } = Array.Empty<double>();
}

public class TdbAnalysisService : ITdbAnalysisService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("TOMODB v2.0");

    private const int RecordBase = 0x0181;
    private const int RecordStride = 1288;
    private const int RecordFloats = 322;
    private const int GridWordOffset = 6;
    private const int ElectrodeRows = 16;
    private const int ElectrodeCols = 16;
    private const int MaxSlots = 354;

    private const int Planes = 7;
    private const int GridSize = 36;
    private const int PlanesBytes = Planes * GridSize * GridSize * 4;
    private const float DisplayCorrection = -0.15f;
    private const int HistogramBins = 32;

    // Full observed range of the corrected 36x36 display planes across the
    // 1650-file MEIK NORM corpus (-0.279 .. 1.091). A fixed axis makes the
    // seven histograms directly comparable; the desktop app auto-ranges each
    // layer, so these are its empirical extremes rather than a configured
    // constant.
    private const double DisplayHistogramLow = -0.30;
    private const double DisplayHistogramHigh = 1.10;

    private static readonly int[] LayerDepthsMm = { 4, 11, 18, 25, 32, 39, 46 };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool IsTdb(byte[] data)
        => data.Length >= Magic.Length && data.AsSpan(0, Magic.Length).SequenceEqual(Magic);

    public bool IsTdbFileName(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName) &&
           fileName.EndsWith(".tdb", StringComparison.OrdinalIgnoreCase);

    public TdbAnalysis Parse(byte[] data)
    {
        if (!IsTdb(data))
        {
            throw new InvalidOperationException("Soubor není měření MEIK TOMODB v2.0 (.tdb).");
        }

        var conditions = DecodeCp1251(data, 0x0081, 0x7F);
        var (side, posture) = ParseConditions(conditions);
        var noise = data.Length >= 0x0102
            ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x0100, 2)) / 100.0
            : 0.0;

        var storedPlanes = TryReadStoredPlanes(data);
        var slices = storedPlanes ?? ReadPlanesFromScanlines(data);
        if (slices.Count == 0)
        {
            throw new InvalidOperationException("V souboru .tdb nejsou žádná naměřená data.");
        }

        // The original renderer uses one shared window across all planes.
        var all = slices
            .SelectMany(s => s.Values)
            .Where(v => float.IsFinite(v) && v != 0f)
            .ToArray();

        double low = 0.0, high = 1.0;
        if (all.Length > 0)
        {
            low = all.Min(v => (double)v);
            high = all.Max(v => (double)v);
            if (high <= low)
            {
                high = low + 1.0;
            }
        }

        // Stored display planes use the fixed corpus-wide axis. The rare
        // scanline fallback carries raw microsiemens, so it keeps its own
        // data range instead of the conductivity scale.
        var histogramLow = storedPlanes != null ? DisplayHistogramLow : low;
        var histogramHigh = storedPlanes != null ? DisplayHistogramHigh : high;
        if (histogramHigh <= histogramLow)
        {
            histogramHigh = histogramLow + 1.0;
        }

        foreach (var slice in slices)
        {
            FillStats(slice, histogramLow, histogramHigh);
        }

        return new TdbAnalysis
        {
            Conditions = conditions,
            Side = side,
            Posture = posture,
            NoiseLevel = noise,
            WindowLow = low,
            WindowHigh = high,
            HistogramLow = histogramLow,
            HistogramHigh = histogramHigh,
            Slices = slices
        };
    }

    public string ToJson(TdbAnalysis analysis)
    {
        var payload = new
        {
            width = GridSize,
            height = GridSize,
            windowLow = analysis.WindowLow,
            windowHigh = analysis.WindowHigh,
            histogramLow = analysis.HistogramLow,
            histogramHigh = analysis.HistogramHigh,
            noiseLevel = analysis.NoiseLevel,
            slices = analysis.Slices.Select(s => new
            {
                index = s.Index,
                depthMm = s.DepthMm,
                width = s.Width,
                height = s.Height,
                min = s.Min,
                max = s.Max,
                mean = s.Mean,
                values = Convert.ToBase64String(ToLittleEndianBytes(s.Values)),
                histogram = new
                {
                    counts = s.HistogramCounts,
                    edges = s.HistogramEdges
                }
            })
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static List<TdbSlice>? TryReadStoredPlanes(byte[] data)
    {
        if (data.Length < PlanesBytes)
        {
            return null;
        }

        var block = data.AsSpan(data.Length - PlanesBytes);
        var values = new float[Planes * GridSize * GridSize];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(block.Slice(i * 4, 4))
                        + DisplayCorrection;
        }

        // A healthy archive has non-zero planes. All-zero tails mean the
        // record was truncated or written by a different producer.
        if (values.All(v => v == 0f || !float.IsFinite(v)))
        {
            return null;
        }

        var slices = new List<TdbSlice>(Planes);
        for (var layer = 0; layer < Planes; layer++)
        {
            var sliceValues = new float[GridSize * GridSize];
            Array.Copy(values, layer * sliceValues.Length, sliceValues, 0, sliceValues.Length);
            slices.Add(new TdbSlice
            {
                Index = layer,
                DepthMm = LayerDepthsMm[layer],
                Width = GridSize,
                Height = GridSize,
                Values = sliceValues
            });
        }

        return slices;
    }

    /// <summary>
    /// Older/foreign records without the stored display planes: build the
    /// seven views from evenly spaced 16x16 electrode grids (nearest-neighbour
    /// enlarged to the 36x36 display grid).
    /// </summary>
    private static List<TdbSlice> ReadPlanesFromScanlines(byte[] data)
    {
        var grids = new List<float[]>();
        var slots = Math.Min((data.Length - RecordBase) / RecordStride, MaxSlots);
        for (var slot = 0; slot < slots; slot++)
        {
            var offset = RecordBase + slot * RecordStride;
            if (offset + RecordFloats * 4 > data.Length)
            {
                break;
            }

            var grid = new float[ElectrodeRows * ElectrodeCols];
            var populated = false;
            for (var i = 0; i < grid.Length; i++)
            {
                var value = BinaryPrimitives.ReadSingleLittleEndian(
                    data.AsSpan(offset + (GridWordOffset + i) * 4, 4));
                grid[i] = value;
                populated |= value != 0f && float.IsFinite(value);
            }

            if (populated)
            {
                grids.Add(grid);
            }
        }

        if (grids.Count == 0)
        {
            return new List<TdbSlice>();
        }

        var selected = new List<float[]>(Planes);
        if (grids.Count <= Planes)
        {
            selected.AddRange(grids);
        }
        else
        {
            for (var i = 0; i < Planes; i++)
            {
                var index = (int)Math.Round(i * (grids.Count - 1.0) / (Planes - 1.0));
                selected.Add(grids[index]);
            }
        }

        var result = new List<TdbSlice>(selected.Count);
        for (var layer = 0; layer < selected.Count; layer++)
        {
            var enlarged = new float[GridSize * GridSize];
            var source = selected[layer];
            for (var row = 0; row < GridSize; row++)
            {
                var sourceRow = Math.Min(row * ElectrodeRows / GridSize, ElectrodeRows - 1);
                for (var col = 0; col < GridSize; col++)
                {
                    var sourceCol = Math.Min(col * ElectrodeCols / GridSize, ElectrodeCols - 1);
                    enlarged[row * GridSize + col] = source[sourceRow * ElectrodeCols + sourceCol];
                }
            }

            result.Add(new TdbSlice
            {
                Index = layer,
                DepthMm = layer < LayerDepthsMm.Length ? LayerDepthsMm[layer] : (layer + 1) * 7,
                Width = GridSize,
                Height = GridSize,
                Values = enlarged
            });
        }

        return result;
    }

    private static void FillStats(TdbSlice slice, double histogramLow, double histogramHigh)
    {
        var values = slice.Values
            .Where(v => float.IsFinite(v) && v != 0f)
            .Select(v => (double)v)
            .ToArray();

        if (values.Length == 0)
        {
            slice.Min = 0;
            slice.Max = 0;
            slice.Mean = 0;
            slice.HistogramCounts = new int[HistogramBins];
            slice.HistogramEdges = BuildEdges(histogramLow, histogramHigh);
            return;
        }

        slice.Min = values.Min();
        slice.Max = values.Max();
        slice.Mean = values.Average();

        var counts = new int[HistogramBins];
        foreach (var value in values)
        {
            var position = (value - histogramLow) / (histogramHigh - histogramLow);
            var bin = (int)Math.Floor(position * HistogramBins);
            if (bin < 0) bin = 0;
            if (bin >= HistogramBins) bin = HistogramBins - 1;
            counts[bin]++;
        }

        slice.HistogramCounts = counts;
        slice.HistogramEdges = BuildEdges(histogramLow, histogramHigh);
    }

    private static double[] BuildEdges(double low, double high)
    {
        var edges = new double[HistogramBins + 1];
        for (var i = 0; i <= HistogramBins; i++)
        {
            edges[i] = low + (high - low) * i / HistogramBins;
        }
        return edges;
    }

    private static byte[] ToLittleEndianBytes(float[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4, 4), values[i]);
        }
        return bytes;
    }

    internal static (string? Side, string? Posture) ParseConditions(string conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions))
        {
            return (null, null);
        }

        var text = conditions.ToLowerInvariant();

        // Russian ("правая/левая", "стоя/лежа/сидя"), English and Czech.
        var right = IndexOfFirst(text, "прав", "right", "prav");
        var left = IndexOfFirst(text, "лев", "left", "lev");
        string? side = null;
        if (right >= 0 && (left < 0 || right < left))
        {
            side = "Right";
        }
        else if (left >= 0)
        {
            side = "Left";
        }

        var lying = IndexOfFirst(text, "леж", "lying", "lež", "lez");
        var standing = IndexOfFirst(text, "стоя", "standing", "stoj");
        var sitting = IndexOfFirst(text, "сидя", "sitting", "sed");
        string? posture = null;
        var best = int.MaxValue;
        if (lying >= 0 && lying < best) { best = lying; posture = "Lying"; }
        if (standing >= 0 && standing < best) { best = standing; posture = "Standing"; }
        if (sitting >= 0 && sitting < best) { posture = "Sitting"; }

        return (side, posture);
    }

    private static int IndexOfFirst(string text, params string[] needles)
    {
        var best = -1;
        foreach (var needle in needles)
        {
            var index = text.IndexOf(needle, StringComparison.Ordinal);
            if (index >= 0 && (best < 0 || index < best))
            {
                best = index;
            }
        }
        return best;
    }

    /// <summary>Minimal CP1251 decoder (Cyrillic block + common punctuation).</summary>
    private static string DecodeCp1251(byte[] data, int offset, int maxLength)
    {
        var builder = new StringBuilder();
        var end = Math.Min(offset + maxLength, data.Length);
        for (var i = offset; i < end && data[i] != 0; i++)
        {
            var b = data[i];
            if (b < 0x80)
            {
                builder.Append((char)b);
            }
            else if (b >= 0xC0)
            {
                builder.Append((char)(0x0410 + (b - 0xC0)));
            }
            else
            {
                builder.Append(b switch
                {
                    0x80 => 'Ђ',
                    0x81 => 'Ѓ',
                    0x82 => '‚',
                    0x83 => 'ѓ',
                    0x84 => '„',
                    0x85 => '…',
                    0x86 => '†',
                    0x87 => '‡',
                    0x88 => '€',
                    0x89 => '‰',
                    0x8A => 'Љ',
                    0x8B => '‹',
                    0x8C => 'Њ',
                    0x8D => 'Ќ',
                    0x8E => 'Ћ',
                    0x8F => 'Џ',
                    0x90 => 'ђ',
                    0x91 => '‘',
                    0x92 => '’',
                    0x93 => '“',
                    0x94 => '”',
                    0x95 => '•',
                    0x96 => '–',
                    0x97 => '—',
                    0x98 => ' ',
                    0x99 => '™',
                    0x9A => 'љ',
                    0x9B => '›',
                    0x9C => 'њ',
                    0x9D => 'ќ',
                    0x9E => 'ћ',
                    0x9F => 'џ',
                    0xA0 => ' ',
                    0xA1 => 'Ў',
                    0xA2 => 'ў',
                    0xA3 => 'Ј',
                    0xA4 => '¤',
                    0xA5 => 'Ґ',
                    0xA6 => '¦',
                    0xA7 => '§',
                    0xA8 => 'Ё',
                    0xA9 => '©',
                    0xAA => 'Є',
                    0xAB => '«',
                    0xAC => '¬',
                    0xAD => '­',
                    0xAE => '®',
                    0xAF => 'Ї',
                    0xB0 => '°',
                    0xB1 => '±',
                    0xB2 => 'І',
                    0xB3 => 'і',
                    0xB4 => 'ґ',
                    0xB5 => 'µ',
                    0xB6 => '¶',
                    0xB7 => '·',
                    0xB8 => 'ё',
                    0xB9 => '№',
                    0xBA => 'є',
                    0xBB => '»',
                    0xBC => 'ј',
                    0xBD => 'Ѕ',
                    0xBE => 'ѕ',
                    0xBF => 'ї',
                    _ => '?'
                });
            }
        }
        return builder.ToString().Trim();
    }
}
