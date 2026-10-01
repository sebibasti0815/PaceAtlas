using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private const string BlsDownloadPage = "https://blsdb.de/download";
    // Headers of the original BLS_4_0_2025_DE.zip used to build our 4.0 offline seed.
    private const string BlsSeedFile = "BLS_4_0_2025_DE.zip";
    private const long BlsSeedLength = 14263306;
    private static readonly DateTimeOffset BlsSeedModified =
        new(2026, 9, 7, 15, 26, 56, TimeSpan.Zero);
    private static string BlsUpdateSettingsPath => Path.Combine(WinUiSettingsFolder, "bls-update-settings.json");
    private bool blsCheckRunning;

    private sealed class BlsUpdateSettings
    {
        public DateTimeOffset LastCheck { get; set; }
    }

    private void CheckBlsData_Click(object sender, RoutedEventArgs e) => _ = CheckBlsDataAsync(true);

    private async Task CheckBlsDataAsync(bool manual)
    {
        if (blsCheckRunning) return;
        var english = selectedLanguage == "en";
        var settings = new BlsUpdateSettings();
        try
        {
            if (File.Exists(BlsUpdateSettingsPath))
                settings = JsonSerializer.Deserialize<BlsUpdateSettings>(File.ReadAllText(BlsUpdateSettingsPath)) ?? settings;
        }
        catch (Exception) { /* A damaged check timestamp must not block the catalog. */ }
        if (!manual && DateTimeOffset.UtcNow - settings.LastCheck < TimeSpan.FromDays(1)) return;

        if (!store.HasBlsCatalog())
        {
            if (manual) await ShowBlsCheckMessageAsync(english
                ? "No BLS catalog is installed for this user. Install the complete Pace Atlas package containing the offline data."
                : "Für diesen Benutzer ist kein BLS-Katalog installiert. Installiere das vollständige Pace-Atlas-Paket mit den Offlinedaten.");
            return;
        }

        blsCheckRunning = true;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PaceAtlas", "1.0"));
            using var page = await client.GetAsync(BlsDownloadPage);
            page.EnsureSuccessStatusCode();
            if (page.Content.Headers.ContentLength > 1048576)
                throw new InvalidDataException("BLS-Downloadseite zu groß.");
            var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
            if (html.Length > 1048576) throw new InvalidDataException("BLS-Downloadseite zu groß.");
            var links = Regex.Matches(html, "href\\s*=\\s*['\"]([^'\"]+\\.zip(?:\\?[^'\"]*)?)['\"]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var archive = links.Cast<Match>().Select(match => new Uri(new Uri(BlsDownloadPage), match.Groups[1].Value))
                .FirstOrDefault(uri => uri.Scheme == Uri.UriSchemeHttps && uri.Host == "blsdb.de" &&
                    Path.GetFileName(uri.AbsolutePath).StartsWith("BLS_", StringComparison.OrdinalIgnoreCase));
            if (archive is null) throw new InvalidDataException("Kein offizielles BLS-Archiv gefunden.");

            using var head = new HttpRequestMessage(HttpMethod.Head, archive);
            using var response = await client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri is not { Scheme: "https", Host: "blsdb.de" })
                throw new InvalidDataException("Ungültige BLS-Downloadadresse.");
            var name = Path.GetFileName(archive.AbsolutePath);
            var length = response.Content.Headers.ContentLength;
            var modified = response.Content.Headers.LastModified;
            if (length is null && modified is null && name.Equals(BlsSeedFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Die offiziellen Dateiinformationen fehlen; ein Vergleich ist nicht möglich.");
            var different = !name.Equals(BlsSeedFile, StringComparison.OrdinalIgnoreCase) ||
                length is not null && length != BlsSeedLength ||
                modified is not null && modified != BlsSeedModified;
            settings.LastCheck = DateTimeOffset.UtcNow;
            Directory.CreateDirectory(WinUiSettingsFolder);
            File.WriteAllText(BlsUpdateSettingsPath, JsonSerializer.Serialize(settings));
            if (different)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = ((FrameworkElement)Content).XamlRoot,
                    Title = english ? "BLS data may have changed" : "BLS-Daten möglicherweise aktualisiert",
                    Content = english
                        ? "The official BLS download differs from the catalog supplied with Pace Atlas. Open the source page? Your personal entries and saved meals are unaffected; this check does not import data."
                        : "Der offizielle BLS-Download unterscheidet sich vom mitgelieferten Katalog. Quellseite öffnen? Eigene Einträge und gespeicherte Mahlzeiten bleiben unberührt; diese Prüfung importiert keine Daten.",
                    PrimaryButtonText = english ? "Open source" : "Quellseite öffnen",
                    CloseButtonText = english ? "Later" : "Später"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    Process.Start(new ProcessStartInfo(BlsDownloadPage) { UseShellExecute = true });
            }
            else if (manual)
            {
                var message = english ? "The official BLS file matches your offline catalog."
                    : "Die offizielle BLS-Datei entspricht deinem Offlinekatalog.";
                await ShowBlsCheckMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            if (manual) await ShowBlsCheckMessageAsync((english ? "BLS check failed: " : "BLS-Prüfung fehlgeschlagen: ") + ex.Message);
        }
        finally { blsCheckRunning = false; }
    }

    private async Task ShowBlsCheckMessageAsync(string message)
    {
        await new ContentDialog
        {
            XamlRoot = ((FrameworkElement)Content).XamlRoot,
            Title = selectedLanguage == "en" ? "BLS data check" : "BLS-Datenprüfung",
            Content = message,
            CloseButtonText = "OK"
        }.ShowAsync();
    }
}
