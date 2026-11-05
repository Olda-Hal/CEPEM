using System.Net.Http.Headers;
using HealthcareAPI.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private const long MaxUploadBytes = 20 * 1024 * 1024;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiController> _logger;

    public AiController(IHttpClientFactory httpClientFactory, ILogger<AiController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Upload a scanned paper intake form and return structured fields for the
    /// digitizer to confirm. The AI service runs only on the private network.
    /// </summary>
    [HttpPost("intake/extract")]
    [RequirePermission("intake.digitise")]
    [EnableRateLimiting("api-write")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> ExtractIntake(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No PDF provided" });
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new { error = "PDF is larger than 20 MB" });
        }

        var signature = new byte[5];
        await using (var source = file.OpenReadStream())
        {
            var read = await source.ReadAsync(signature, cancellationToken);
            if (read < signature.Length || !signature.AsSpan().SequenceEqual("%PDF-"u8))
            {
                return BadRequest(new { error = "File is not a PDF" });
            }
        }

        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "file", file.FileName);

            var client = _httpClientFactory.CreateClient("AiService");
            var response = await client.PostAsync("/api/intake/extract", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI intake extraction failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "AI service is unavailable. The PDF was not processed." });
        }
    }
}
