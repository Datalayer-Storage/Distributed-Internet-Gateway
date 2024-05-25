using Microsoft.AspNetCore.Mvc;

namespace dig.server;

public class UploadRequest
{
    public string StoreId { get; set; } = string.Empty;
    public string FullTreeFilename { get; set; } = string.Empty;
}

[ApiController]
[Route("[controller]")]
public class ApiController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("api root");
    }

    [HttpGet("healthz")]
    public IActionResult Test()
    {
        return Ok(new { status = "aok" });
    }

    // this needs to be implemented for the plugin to work
    // but we are using it so it just returns a 200 status code
    [HttpPost("add_missing_files")]
    public IActionResult AddMissingFiles()
    {
        return Ok(new { status = "aok" });
    }

    // This just needs to return a 200 status code
    [HttpPost("handle_upload")]
    public IActionResult HandleUpload()
    {
        return Ok(new { status = "aok" });
    }

    [HttpPost("plugin_info")]
    public IActionResult PluginInfo()
    {
        try
        {
            var info = new
            {
                name = "S3 Plugin For DIG",
                version = "1.0.0",
                description = "A plugin to process tree files into cache"
            };

            return Ok(info);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error retrieving plugin info: {ex.Message}");
            return StatusCode(500, new { error = "Failed to retrieve plugin information" });
        }
    }

    [HttpPost("upload")]
    public IActionResult Upload([FromBody] UploadRequest request)
    {
        if (request == null)
        {
            return BadRequest("Invalid request");
        }

        try
        {
            return Ok(new { status = "aok" });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error handling upload: {ex.Message}");
            return StatusCode(500, new { error = "Failed to handle upload" });
        }
    }
}
