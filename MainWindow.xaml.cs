using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace CrossoutDBUploader;

public partial class MainWindow : Window
{
    private string AppDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrossoutDB"
    );

    private string ConfigPath => Path.Combine(AppDir, "config.json");
    private string SentPath => Path.Combine(AppDir, "sent.json");

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(AppDir);
        Log("Config file is at : " + AppDir);
        LoadConfig();
    }

    private void Log(string text)
    {
        LogBox.AppendText(text + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    private Config LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            var cfg = new Config { ApiUrl = "https://crossoutdb.com/api/v1/logs/upload" };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath))!;
        ApiKeyBox.Text = config.ApiKey;
        return config;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var cfg = LoadConfig();
        var config = new Config
        {
            ApiKey = ApiKeyBox.Text,
            ApiUrl = cfg.ApiUrl
        };

        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        Log("API key saved");
    }

    private async void SendLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath))!;

            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                Log("API key missing");
                return;
            }

            var sent = LoadSent();

            var logRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Targem\Crossout\logs"
            );

            if (!Directory.Exists(logRoot))
            {
                Log("✖ Crossout log folder not found");
                return;
            }

            var dirs = Directory.GetDirectories(logRoot).OrderBy(d => d).ToList();

            if (dirs.Count <= 1)
            {
                Log("Nothing to send");
                return;
            }

            dirs.RemoveAt(dirs.Count - 1); // ignore dernier

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + config.ApiKey);

            int uploaded = 0;
            int skipped = 0;

            foreach (var dir in dirs)
            {
                var folderName = Path.GetFileName(dir);

                if (sent.Contains(folderName))
                {
                    skipped++;
                    Log($"Already sent {folderName}");
                    continue;
                }

                var combatPath = Path.Combine(dir, "combat.log");
                var gamePath = Path.Combine(dir, "game.log");

                if (!File.Exists(combatPath) || !File.Exists(gamePath))
                {
                    Log($"Missing logs in {folderName}");
                    continue;
                }

                Log($"⬆ Uploading {folderName}...");

                try
                {
                    using var form = new MultipartFormDataContent();

                    form.Add(new StringContent(folderName), "folderName");

                    form.Add(new StreamContent(File.OpenRead(combatPath)), "combat_log_file", "combat.log");
                    form.Add(new StreamContent(File.OpenRead(gamePath)), "game_log_file", "game.log");

                    var response = await client.PostAsync(config.ApiUrl, form);

                    if (response.IsSuccessStatusCode)
                    {
                        uploaded++;
                        sent.Add(folderName);
                        SaveSent(sent);
                        Log($"Uploaded {folderName}");
                    }
                    else
                    {
                        Log($"Failed {folderName} ({response.StatusCode})");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error {folderName}: {ex.Message}");
                }
            }

            Log($"Done. Uploaded: {uploaded}, Skipped: {skipped}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }
    }

    private HashSet<string> LoadSent()
    {
        if (!File.Exists(SentPath))
            return new HashSet<string>();

        return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(SentPath))!;
    }

    private void SaveSent(HashSet<string> sent)
    {
        File.WriteAllText(SentPath, JsonSerializer.Serialize(sent, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public class Config
{
    public string ApiKey { get; set; } = "";
    public string ApiUrl { get; set; } = "";
}
