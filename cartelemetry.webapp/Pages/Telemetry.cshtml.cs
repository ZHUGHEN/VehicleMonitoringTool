using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTelemetry.WebApp.Pages;

public class TelemetryModel : PageModel
{
    private readonly ILogger<TelemetryModel> _logger;

    public TelemetryModel(ILogger<TelemetryModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        _logger.LogInformation("Telemetry dashboard accessed");
    }
}