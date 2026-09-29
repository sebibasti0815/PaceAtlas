using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private static string UpdateSettingsPath => Path.Combine(WinUiSettingsFolder, "update-settings.json");
    private const string UpdateRepository = "https://github.com/sebibasti0815/PaceAtlas";
    private bool updateCheckRunning;

    private sealed class UpdateSettings
    {
        public UpdateSettings() { }
        public DateTimeOffset LastCheck { get; set; }
    }

    private sealed class GitHubRelease
    {
        public GitHubRelease() { }
        public string Tag_Name { get; set; } = "";
        public string Html_Url { get; set; } = "";
        public bool Draft { get; set; }
        public bool Prerelease { get; set; }
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    private sealed class GitHubAsset
    {
        public GitHubAsset() { }
        public string Name { get; set; } = "";
        public string Browser_Download_Url { get; set; } = "";
    }

    private static bool TryGitHubRepository(string input, out Uri? endpoint)
    {
        endpoint = null;
        if (!Uri.TryCreate(input.TrimEnd('/'), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com" ||
            uri.UserInfo.Length != 0 || uri.Port != 443) return false;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2 || parts.Any(part => part.Length == 0 ||
            part.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))) return false;
        endpoint = new Uri($"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases?per_page=100");
        return true;
    }

    private static UpdateSettings LoadUpdateSettings()
    {
        try
        {
            if (File.Exists(UpdateSettingsPath))
                return JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(UpdateSettingsPath)) ?? new();
        }
        catch (Exception) { /* A damaged setting must not prevent startup. */ }
        return new();
    }

    private static void SaveUpdateSettings(UpdateSettings settings)
    {
        Directory.CreateDirectory(WinUiSettingsFolder);
        File.WriteAllText(UpdateSettingsPath, JsonSerializer.Serialize(settings));
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (updateCheckRunning) return;
        var settings = LoadUpdateSettings();
        var english = selectedLanguage == "en";
        if (!TryGitHubRepository(UpdateRepository, out var source))
        {
            if (manual) await ShowUpdateMessageAsync(english ? "Enter a GitHub repository URL, e.g. https://github.com/owner/repository." :
                "Bitte die GitHub-Repository-Adresse eintragen, z. B. https://github.com/owner/repository.");
            return;
        }
        if (!manual && DateTimeOffset.UtcNow - settings.LastCheck < TimeSpan.FromDays(1)) return;

        updateCheckRunning = true;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PaceAtlas", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("Update-Adresse muss HTTPS verwenden.");
            if (response.Content.Headers.ContentLength > 4194304)
                throw new InvalidDataException("Update-Datei zu groß.");
            using var stream = await response.Content.ReadAsStreamAsync();
            using var limited = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                if (limited.Length + read > 4194304) throw new InvalidDataException("Update-Datei zu groß.");
                limited.Write(buffer, 0, read);
            }
            limited.Position = 0;
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(limited,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            var newest = releases.Where(r => !r.Draft && !r.Prerelease &&
                Version.TryParse(r.Tag_Name.TrimStart('v', 'V'), out var parsed) && parsed.Build >= 0)
                .Select(r => (Release: r, Version: Version.Parse(r.Tag_Name.TrimStart('v', 'V'))))
                .OrderByDescending(r => r.Version.Major)
                .ThenByDescending(r => r.Version.Minor)
                .ThenByDescending(r => r.Version.Build)
                .FirstOrDefault();
            if (newest.Release is null)
            {
                settings.LastCheck = DateTimeOffset.UtcNow;
                SaveUpdateSettings(settings);
                if (manual) await ShowUpdateMessageAsync(english ? "No published release with a version tag was found." :
                    "Es wurde noch keine veröffentlichte Release mit Versions-Tag gefunden.");
                return;
            }
            var release = newest.Release;
            var latest = newest.Version;
            if (!Uri.TryCreate(release.Html_Url, UriKind.Absolute, out var page) ||
                page.Scheme != Uri.UriSchemeHttps || page.Host != "github.com")
                throw new InvalidDataException("Ungültige GitHub-Release.");

            var asset = release.Assets.FirstOrDefault(a => a.Name.Equals($"PaceAtlas-Setup-{latest.ToString(3)}-win-x64.exe", StringComparison.OrdinalIgnoreCase));
            var target = asset is not null && Uri.TryCreate(asset.Browser_Download_Url, UriKind.Absolute, out var download) &&
                download.Scheme == Uri.UriSchemeHttps && download.Host == "github.com" ? download : page;

            settings.LastCheck = DateTimeOffset.UtcNow;
            SaveUpdateSettings(settings);
            var current = typeof(MainWindow).Assembly.GetName().Version ?? new Version(0, 0, 0);
            if (latest.Major > current.Major ||
                latest.Major == current.Major && (latest.Minor > current.Minor ||
                latest.Minor == current.Minor && latest.Build > current.Build))
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = ((FrameworkElement)Content).XamlRoot,
                    Title = english ? "Update available" : "Update verfügbar",
                    Content = english ? $"Version {latest} is available (installed: {current.ToString(3)}). Open the download page?" :
                        $"Version {latest} ist verfügbar (installiert: {current.ToString(3)}). Downloadseite öffnen?",
                    PrimaryButtonText = english ? "Open download" : "Download öffnen",
                    CloseButtonText = english ? "Later" : "Später"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true });
            }
            else if (manual)
                await ShowUpdateMessageAsync(english ? "You have the latest version." : "Du hast die aktuelle Version.");
        }
        catch (Exception ex)
        {
            if (manual) await ShowUpdateMessageAsync((english ? "Update check failed: " : "Update-Prüfung fehlgeschlagen: ") + ex.Message);
        }
        finally { updateCheckRunning = false; }
    }

    private async Task ShowUpdateMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = ((FrameworkElement)Content).XamlRoot,
            Title = selectedLanguage == "en" ? "Update check" : "Update-Prüfung",
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
