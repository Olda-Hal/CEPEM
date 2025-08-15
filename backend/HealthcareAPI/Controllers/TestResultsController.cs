using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestResultsController : ControllerBase
{
    private readonly string _testResultsPath = "/app/test-results";

    [HttpGet("summary")]
    public async Task<IActionResult> GetTestSummary()
    {
        try
        {
            var summaryPath = Path.Combine(_testResultsPath, "reports", "test-summary.json");
            
            if (!System.IO.File.Exists(summaryPath))
            {
                return Ok(new { 
                    message = "No test results available yet",
                    timestamp = DateTime.UtcNow,
                    services = new Dictionary<string, object>()
                });
            }

            var summaryContent = await System.IO.File.ReadAllTextAsync(summaryPath);
            var summary = JsonSerializer.Deserialize<object>(summaryContent);
            
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("details/{serviceName}")]
    public async Task<IActionResult> GetServiceDetails(string serviceName)
    {
        try
        {
            var reportsPath = Path.Combine(_testResultsPath, "reports");
            
            var debugInfo = new {
                testResultsPath = _testResultsPath,
                reportsPath,
                testResultsExists = Directory.Exists(_testResultsPath),
                reportsExists = Directory.Exists(reportsPath)
            };
            
            if (!Directory.Exists(reportsPath))
            {
                return NotFound(new { message = "Test reports directory not found", debug = debugInfo });
            }

            var serviceFiles = new List<object>();
            
            try 
            {
                // Get all files from reports directory
                var files = Directory.GetFiles(reportsPath);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (!fileName.StartsWith("."))
                    {
                        serviceFiles.Add(new {
                            name = fileName,
                            path = file,
                            size = new FileInfo(file).Length,
                            modified = System.IO.File.GetLastWriteTimeUtc(file),
                            type = GetFileType(fileName)
                        });
                    }
                }
            }
            catch (Exception fileEx)
            {
                return StatusCode(500, new { 
                    error = "Error reading files", 
                    details = fileEx.Message,
                    debug = debugInfo 
                });
            }

            return Ok(new {
                serviceName,
                files = serviceFiles,
                count = serviceFiles.Count,
                debug = debugInfo
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    private string GetFileType(string fileName)
    {
        return fileName.ToLower() switch
        {
            var name when name.EndsWith(".xml") => "test-results",
            var name when name.EndsWith(".trx") => "test-results", 
            var name when name.EndsWith(".json") => "summary",
            var name when name.EndsWith(".html") => "report",
            _ => "unknown"
        };
    }

    [HttpGet("file/{serviceName}/{fileName}")]
    public async Task<IActionResult> GetTestFile(string serviceName, string fileName)
    {
        try
        {
            // Try direct file path first
            var directFilePath = Path.Combine(_testResultsPath, "reports", fileName);
            
            if (System.IO.File.Exists(directFilePath))
            {
                var contentType = GetContentType(fileName);
                var content = await System.IO.File.ReadAllTextAsync(directFilePath);
                return Content(content, contentType);
            }
            
            // Try with service prefix (legacy support)
            var prefixedFilePath = Path.Combine(_testResultsPath, "reports", $"{serviceName}_{fileName}");
            
            if (System.IO.File.Exists(prefixedFilePath))
            {
                var contentType = GetContentType(fileName);
                var content = await System.IO.File.ReadAllTextAsync(prefixedFilePath);
                return Content(content, contentType);
            }

            return NotFound(new { message = "File not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    private string GetContentType(string fileName)
    {
        return fileName.ToLower() switch
        {
            var name when name.EndsWith(".xml") => "application/xml",
            var name when name.EndsWith(".json") => "application/json",
            var name when name.EndsWith(".html") => "text/html",
            var name when name.EndsWith(".trx") => "application/xml",
            _ => "text/plain"
        };
    }

    [HttpGet("coverage/{serviceName}")]
    public async Task<IActionResult> GetCoverageReport(string serviceName)
    {
        try
        {
            var coverageHtmlPath = Path.Combine(_testResultsPath, "reports", $"{serviceName}_coverage_html", "index.html");
            
            if (System.IO.File.Exists(coverageHtmlPath))
            {
                var content = await System.IO.File.ReadAllTextAsync(coverageHtmlPath);
                return Content(content, "text/html");
            }

            // Try to find coverage JSON
            var coverageJsonPath = Path.Combine(_testResultsPath, "reports", $"{serviceName}_coverage-final.json");
            if (System.IO.File.Exists(coverageJsonPath))
            {
                var content = await System.IO.File.ReadAllTextAsync(coverageJsonPath);
                return Content(content, "application/json");
            }

            return NotFound(new { message = "No coverage report found for service" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("live-status")]
    public IActionResult GetLiveStatus()
    {
        try
        {
            var reportsPath = Path.Combine(_testResultsPath, "reports");
            var testResultsExist = Directory.Exists(reportsPath);
            
            var fileCount = 0;
            if (testResultsExist)
            {
                fileCount = Directory.GetFiles(reportsPath).Length;
            }
            
            var services = new[] { "healthcare-api", "database-api", "ai-service", "frontend" };
            var status = services.ToDictionary(service => service, service => new {
                hasResults = fileCount > 0,
                fileCount = fileCount,
                lastUpdate = fileCount > 0 && testResultsExist ? 
                    Directory.GetFiles(reportsPath).Max(f => System.IO.File.GetLastWriteTimeUtc(f)) : (DateTime?)null
            });

            return Ok(new {
                timestamp = DateTime.UtcNow,
                testResultsPath = testResultsExist,
                services = status
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
