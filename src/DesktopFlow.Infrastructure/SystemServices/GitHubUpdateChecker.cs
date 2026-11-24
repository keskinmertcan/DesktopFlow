using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using DesktopFlow.Core;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.Infrastructure.SystemServices;

public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private readonly IAppLogger _logger;
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    public GitHubUpdateChecker(IAppLogger logger)
    {
        _logger = logger;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DesktopFlow", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var requestUri = ProductInfo.RepositoryUrl.TrimEnd('/') + "/releases/latest";
        var apiUri = requestUri.Replace("https://github.com/", "https://api.github.com/repos/", StringComparison.OrdinalIgnoreCase);
        try
        {
            using var response = await _http.GetAsync(apiUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    true,
                    false,
                    null,
                    "No published release was found. DesktopFlow keeps working offline.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var tag = document.RootElement.TryGetProperty("tag_name", out var tagProperty)
                ? tagProperty.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return new UpdateCheckResult(true, false, null, "The release information did not include a version.");
            }

            var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
            var latestText = tag.Trim().TrimStart('v', 'V');
            var updateAvailable = Version.TryParse(latestText, out var latest) && latest > current;
            var message = updateAvailable
                ? $"Version {latestText} is available."
                : "You are using the latest published version.";
            return new UpdateCheckResult(true, updateAvailable, latestText, message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.Info("Update check did not reach GitHub.");
            return new UpdateCheckResult(false, false, null, "Could not reach GitHub. DesktopFlow keeps working offline.");
        }
    }
}
