using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DatabaseAPI.Services;

public static class DocumentExtractionStatus
{
    public const string Pending = "Pending";
    public const string Extracted = "Extracted";
    public const string NoText = "NoText";
    public const string Unsupported = "Unsupported";
    public const string Unavailable = "Unavailable";
    public const string Failed = "Failed";
}

public sealed record DocumentTextExtractionResult(
    string Status,
    string? Text,
    string? Error);

public interface IDocumentTextExtractionService
{
    Task<DocumentTextExtractionResult> ExtractAsync(
        byte[] data, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extracts a searchable text layer from uploaded documents. PDFs use
/// pdftotext first; scanned PDFs fall back to pdftoppm + Tesseract. Images
/// go straight to Tesseract. Everything runs as an external process with
/// timeouts and a bounded page count; missing tools degrade to Unavailable.
/// </summary>
public class DocumentTextExtractionService : IDocumentTextExtractionService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".webp"
    };

    private readonly bool _enabled;
    private readonly string _tesseractPath;
    private readonly string _pdfToTextPath;
    private readonly string _pdfToPpmPath;
    private readonly string _languages;
    private readonly int _maxPages;
    private readonly int _maxTextLength;
    private readonly TimeSpan _timeout;
    private readonly ILogger<DocumentTextExtractionService> _logger;

    public DocumentTextExtractionService(
        IConfiguration configuration,
        ILogger<DocumentTextExtractionService> logger)
    {
        _logger = logger;
        _enabled = configuration.GetValue("Ocr:Enabled", true);
        _tesseractPath = configuration["Ocr:TesseractPath"] ?? "tesseract";
        _pdfToTextPath = configuration["Ocr:PdfToTextPath"] ?? "pdftotext";
        _pdfToPpmPath = configuration["Ocr:PdfToPpmPath"] ?? "pdftoppm";
        _languages = configuration["Ocr:Languages"] ?? "ces+eng";
        _maxPages = Math.Clamp(configuration.GetValue("Ocr:MaxPages", 5), 1, 20);
        _maxTextLength = Math.Clamp(configuration.GetValue("Ocr:MaxTextLength", 200_000), 1000, 1_000_000);
        _timeout = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Ocr:TimeoutSeconds", 60), 5, 300));
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        byte[] data, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        var isPdf = extension == ".pdf";
        var isImage = ImageExtensions.Contains(extension);
        if (!isPdf && !isImage)
        {
            return new DocumentTextExtractionResult(DocumentExtractionStatus.Unsupported, null, null);
        }
        if (!_enabled)
        {
            return new DocumentTextExtractionResult(DocumentExtractionStatus.Unavailable, null, "OCR is disabled");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"cepem-ocr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var inputPath = Path.Combine(tempDirectory, $"input{extension}");

        try
        {
            await File.WriteAllBytesAsync(inputPath, data, cancellationToken);
            return isPdf
                ? await ExtractFromPdfAsync(inputPath, tempDirectory, cancellationToken)
                : await ExtractFromImageAsync(inputPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Text extraction failed for {FileName}", fileName);
            return new DocumentTextExtractionResult(DocumentExtractionStatus.Failed, null, ex.Message);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not clean OCR temp directory {Path}", tempDirectory);
            }
        }
    }

    private async Task<DocumentTextExtractionResult> ExtractFromPdfAsync(
        string inputPath, string tempDirectory, CancellationToken cancellationToken)
    {
        var textResult = await RunAsync(
            _pdfToTextPath,
            new[] { "-layout", "-enc", "UTF-8", inputPath, "-" },
            cancellationToken);

        if (textResult.MissingTool)
        {
            return new DocumentTextExtractionResult(
                DocumentExtractionStatus.Unavailable, null, textResult.StdErr);
        }

        var text = Normalize(textResult.StdOut);
        if (text.Length >= 20)
        {
            return new DocumentTextExtractionResult(
                DocumentExtractionStatus.Extracted, Truncate(text), null);
        }

        // Scanned PDF: render up to MaxPages pages and OCR them.
        var prefix = Path.Combine(tempDirectory, "page");
        var renderResult = await RunAsync(
            _pdfToPpmPath,
            new[]
            {
                "-r", "200", "-png",
                "-f", "1",
                "-l", _maxPages.ToString(),
                inputPath,
                prefix
            },
            cancellationToken);

        if (renderResult.MissingTool)
        {
            return new DocumentTextExtractionResult(
                DocumentExtractionStatus.Unavailable, null, renderResult.StdErr);
        }
        if (renderResult.ExitCode != 0)
        {
            return new DocumentTextExtractionResult(
                DocumentExtractionStatus.Failed, null, Normalize(renderResult.StdErr));
        }

        var images = Directory.GetFiles(tempDirectory, "page-*.png")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(_maxPages)
            .ToList();
        if (images.Count == 0)
        {
            return new DocumentTextExtractionResult(DocumentExtractionStatus.NoText, null, null);
        }

        var builder = new StringBuilder();
        foreach (var image in images)
        {
            var ocr = await RunAsync(
                _tesseractPath,
                new[] { image, "stdout", "-l", _languages },
                cancellationToken);

            if (ocr.MissingTool)
            {
                return new DocumentTextExtractionResult(
                    DocumentExtractionStatus.Unavailable, null, ocr.StdErr);
            }
            if (ocr.ExitCode == 0)
            {
                builder.AppendLine(Normalize(ocr.StdOut));
            }
        }

        var ocrText = Normalize(builder.ToString());
        return ocrText.Length >= 2
            ? new DocumentTextExtractionResult(DocumentExtractionStatus.Extracted, Truncate(ocrText), null)
            : new DocumentTextExtractionResult(DocumentExtractionStatus.NoText, null, null);
    }

    private async Task<DocumentTextExtractionResult> ExtractFromImageAsync(
        string inputPath, CancellationToken cancellationToken)
    {
        var ocr = await RunAsync(
            _tesseractPath,
            new[] { inputPath, "stdout", "-l", _languages },
            cancellationToken);

        if (ocr.MissingTool)
        {
            return new DocumentTextExtractionResult(
                DocumentExtractionStatus.Unavailable, null, ocr.StdErr);
        }
        if (ocr.ExitCode != 0)
        {
            return new DocumentTextExtractionResult(
                DocumentExtractionStatus.Failed, null, Normalize(ocr.StdErr));
        }

        var text = Normalize(ocr.StdOut);
        return text.Length >= 2
            ? new DocumentTextExtractionResult(DocumentExtractionStatus.Extracted, Truncate(text), null)
            : new DocumentTextExtractionResult(DocumentExtractionStatus.NoText, null, null);
    }

    private async Task<ProcessResult> RunAsync(
        string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            return new ProcessResult(-1, string.Empty, ex.Message, MissingTool: true);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not kill timed-out OCR process {Executable}", executable);
            }

            return new ProcessResult(-1, await SafeReadAsync(stdoutTask), "OCR process timed out", false);
        }

        return new ProcessResult(
            process.ExitCode,
            await SafeReadAsync(stdoutTask),
            await SafeReadAsync(stderrTask),
            false);
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string Truncate(string value) =>
        value.Length <= _maxTextLength ? value : value[.._maxTextLength];

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, bool MissingTool);
}
