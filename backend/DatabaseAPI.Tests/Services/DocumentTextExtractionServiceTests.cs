using System.Text;
using DatabaseAPI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// PDF text-layer extraction and image OCR. The suite uses the host's
/// pdftotext/Tesseract; when a tool is missing the corresponding assertion is
/// skipped instead of failing the build on a minimal CI image.
/// </summary>
public class DocumentTextExtractionServiceTests
{
    private static DocumentTextExtractionService CreateService(
        string? pdfToText = null,
        string? tesseract = null,
        bool enabled = true,
        string languages = "eng")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ocr:Enabled"] = enabled.ToString(),
                ["Ocr:PdfToTextPath"] = pdfToText ?? "pdftotext",
                ["Ocr:PdfToPpmPath"] = "pdftoppm",
                ["Ocr:TesseractPath"] = tesseract ?? "tesseract",
                ["Ocr:Languages"] = languages,
                ["Ocr:MaxPages"] = "2",
                ["Ocr:TimeoutSeconds"] = "60"
            })
            .Build();
        return new DocumentTextExtractionService(
            configuration, NullLogger<DocumentTextExtractionService>.Instance);
    }

    private static bool ToolExists(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, name))
            .Any(File.Exists);
    }

    [Fact]
    public async Task PdfWithTextLayer_IsExtracted()
    {
        if (!ToolExists("pdftotext"))
        {
            return; // minimal CI image without poppler
        }

        var service = CreateService();
        var pdf = BuildSimplePdf("Hello OCR");

        var result = await service.ExtractAsync(pdf, "sample.pdf");

        Assert.Equal(DocumentExtractionStatus.Extracted, result.Status);
        Assert.Contains("Hello OCR", result.Text);
    }

    [Fact]
    public async Task Image_IsOcred()
    {
        if (!ToolExists("tesseract"))
        {
            return; // minimal CI image without Tesseract
        }

        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ocr-sample.png");
        if (!File.Exists(fixture))
        {
            return;
        }

        var service = CreateService();
        var bytes = await File.ReadAllBytesAsync(fixture);

        var result = await service.ExtractAsync(bytes, "scan.png");

        Assert.Equal(DocumentExtractionStatus.Extracted, result.Status);
        Assert.Contains("Hello", result.Text);
    }

    [Fact]
    public async Task UnsupportedExtension_IsReported()
    {
        var service = CreateService();

        var result = await service.ExtractAsync(new byte[] { 1, 2, 3 }, "measurement.tdb");

        Assert.Equal(DocumentExtractionStatus.Unsupported, result.Status);
    }

    [Fact]
    public async Task DisabledOcr_IsReported()
    {
        var service = CreateService(enabled: false);

        var result = await service.ExtractAsync(new byte[] { 1, 2, 3 }, "scan.png");

        Assert.Equal(DocumentExtractionStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task MissingPdfTool_IsReportedAsUnavailable()
    {
        var service = CreateService(pdfToText: "/definitely/missing/pdftotext");

        var result = await service.ExtractAsync(BuildSimplePdf("Hello"), "sample.pdf");

        Assert.Equal(DocumentExtractionStatus.Unavailable, result.Status);
    }

    /// <summary>Builds a minimal one-page PDF with a Helvetica text object.</summary>
    private static byte[] BuildSimplePdf(string text)
    {
        var content = $"BT /F1 24 Tf 20 100 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(builder.Length);
            builder.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = builder.Length;
        builder.Append($"xref\n0 {objects.Length + 1}\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append($"{offset:D10} 00000 n \n");
        }
        builder.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        builder.Append($"startxref\n{xrefOffset}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
