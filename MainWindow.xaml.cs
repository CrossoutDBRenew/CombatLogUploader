using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CrossoutDBUploader;

public partial class MainWindow : Window
{
    private string AppDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrossoutDB"
    );

    private string ConfigPath => Path.Combine(AppDir, "config.json");
    private string SentPath => Path.Combine(AppDir, "sent.json");

    private Config _config = new Config();

    private DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(AppDir);
        Log("Config folder: " + AppDir);
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(60);
        _timer.Tick += async (_, __) => await SendLogsAsync(true);

        try
        {
            LoadConfig();

            if (_config.AutoSend)
            {
                _timer.Start();
                Log("Auto send enabled (startup)");
                _ = SendLogsAsync(true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Startup error:\n" + ex);
        }
    }

    // ================= LOG UI =================

    private void Log(string text)
    {
        LogBox.AppendText(text + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    // ================= CONFIG =================

    private void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                _config = new Config
                {
                    ApiUrl = "https://crossoutdb.com/api/v1/logs/upload",
                    ApiKey = "",
                    AutoSend = false,
                    AutoStartWithWindows = false
                };

                File.WriteAllText(
                    ConfigPath,
                    JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true })
                );

                Log("Config created");
            }
            else
            {
                var json = File.ReadAllText(ConfigPath);

                _config = string.IsNullOrWhiteSpace(json)
                    ? new Config { ApiUrl = "https://crossoutdb.com/api/v1/logs/upload" }
                    : JsonSerializer.Deserialize<Config>(json) ?? new Config { ApiUrl = "https://crossoutdb.com/api/v1/logs/upload" };

                Log("Config loaded");
            }

            ShowMaskedApiKey(_config.ApiKey ?? "");

            AutoSendCheckBox.IsChecked = _config.AutoSend;
            AutoStartCheckBox.IsChecked = _config.AutoStartWithWindows;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Config error:\n" + ex);

            _config = new Config
            {
                ApiUrl = "https://crossoutdb.com/api/v1/logs/upload"
            };
        }
    }

    private void ApplyAutoStart()
    {
        try
        {
            string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);

            if (key == null)
            {
                Log("Registry access failed");
                return;
            }

            if (_config.AutoStartWithWindows)
            {
                key.SetValue(
                    "CrossoutDBUploader",
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                );
                Log("Auto start enabled");
            }
            else
            {
                key.DeleteValue("CrossoutDBUploader", false);
                Log("Auto start disabled");
            }
        }
        catch (Exception ex)
        {
            Log("Auto start error: " + ex.Message);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var entered = ApiKeyBox.Password?.Trim();

        if (!string.IsNullOrEmpty(entered) && !entered.Contains("•"))
        {
            _config.ApiKey = entered;
        }

        _config.AutoSend = AutoSendCheckBox.IsChecked == true;
        _config.AutoStartWithWindows = AutoStartCheckBox.IsChecked == true;

        SaveConfig();
        ApplyAutoStart();

        ShowMaskedApiKey(_config.ApiKey);

        Log("Config saved");
    }

    private void SaveConfig()
    {
        File.WriteAllText(
            ConfigPath,
            JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true })
        );
    }

    // ================= SENT JSON =================

    private HashSet<string> LoadSent()
    {
        if (!File.Exists(SentPath))
            return new HashSet<string>();

        return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(SentPath))!;
    }

    private void SaveSent(HashSet<string> sent)
    {
        File.WriteAllText(
            SentPath,
            JsonSerializer.Serialize(sent, new JsonSerializerOptions { WriteIndented = true })
        );
    }

    // ================= RESCAN =================

    private void Rescan_Click(object sender, RoutedEventArgs e)
    {
        RescanLogs();
    }

    private void RescanLogs()
    {
        var logRoot = GetLogRoot();

        if (!Directory.Exists(logRoot))
        {
            Log("Crossout log folder not found");
            return;
        }

        var sent = LoadSent();

        var dirs = Directory.GetDirectories(logRoot)
                            .OrderBy(d => d)
                            .ToList();

        if (dirs.Count > 0)
            dirs.RemoveAt(dirs.Count - 1); // ignore dernier

        int ready = 0;

        foreach (var dir in dirs)
        {
            var folderName = Path.GetFileName(dir);

            if (sent.Contains(folderName))
                continue;

            var combat = Path.Combine(dir, "combat.log");
            var game = Path.Combine(dir, "game.log");

            if (File.Exists(combat) && File.Exists(game))
                ready++;
        }

        Log($"Logs ready to send: {ready}");
    }

    // ================= SEND BUTTON =================

    private async void SendLogs_Click(object sender, RoutedEventArgs e)
    {
        await SendLogsAsync(false);
    }

    private async Task SendLogsAsync(bool isAuto)
    {
        try
        {
            var config = _config;

            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                Log("API key missing");
                return;
            }

            var sent = LoadSent();
            var logRoot = GetLogRoot();

            if (!Directory.Exists(logRoot))
            {
                Log("Crossout log folder not found");
                return;
            }

            var dirs = Directory.GetDirectories(logRoot)
                                .OrderBy(d => d)
                                .ToList();

            if (dirs.Count <= 1)
            {
                if (!isAuto)
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
                    continue;
                }

                var combatPath = Path.Combine(dir, "combat.log");
                var gamePath = Path.Combine(dir, "game.log");

                if (!File.Exists(combatPath) || !File.Exists(gamePath))
                    continue;

                Log($"⬆ Uploading {folderName}...");

                try
                {
                    using var form = new MultipartFormDataContent();

                    form.Add(new StringContent(folderName), "folderName");

                    form.Add(
                        new StreamContent(File.OpenRead(combatPath)),
                        "combat_log_file",
                        "combat.log"
                    );

                    form.Add(
                        new StreamContent(File.OpenRead(gamePath)),
                        "game_log_file",
                        "game.log"
                    );

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

            if (!isAuto)
                Log($"Done. Uploaded: {uploaded}, Skipped: {skipped}");
        }
        catch (Exception ex)
        {
            Log("Fatal error: " + ex.Message);
        }
    }

    private void ShowMaskedApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            ApiKeyBox.Password = "";
            return;
        }

        var visible = apiKey.Length > 6
            ? apiKey.Substring(0, 6) + "••••••"
            : "••••••";

        ApiKeyBox.Password = visible;
    }

    // ================= AUTO SEND =================

    private void AutoSend_Checked(object sender, RoutedEventArgs e)
    {
        if (_config == null || _timer == null) return;

        _config.AutoSend = true;
        _timer.Start();
        Log("Auto send enabled (60s)");
    }

    private void AutoSend_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_config == null || _timer == null) return;

        _config.AutoSend = false;
        _timer.Stop();
        Log("Auto send disabled");
    }

    // ================= HELPERS =================

    private string GetLogRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Targem\Crossout\logs"
        );
    }
}

// ================= CONFIG CLASS =================

public class Config
{
    public string ApiKey { get; set; } = "";
    public string ApiUrl { get; set; } = "";
    public bool AutoSend { get; set; } = false;
    public bool AutoStartWithWindows { get; set; } = false;
}