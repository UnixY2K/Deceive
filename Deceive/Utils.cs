using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Deceive;

internal static class Utils
{
    internal static string DeceiveVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version is null)
                return "v0.0.0";
            return "v" + version.Major + "." + version.Minor + "." + version.Build;
        }
    }

    /**
     * Asynchronously checks if the current version of Deceive is the latest version.
     * If not, and the user has not dismissed the message before, an alert is shown.
     */
    public static async Task<String?> CheckForUpdatesAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deceive", DeceiveVersion));

            var response =
                await httpClient.GetAsync(new Uri("https://api.github.com/repos/molenzwiebel/Deceive/releases/latest")).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<JsonNode>(content);
            var latestVersion = release?["tag_name"]?.ToString();

            // If failed to fetch or already latest or newer, return.
            if (latestVersion is null)
                return null;
            var githubVersion = new Version(latestVersion.Replace("v", "", StringComparison.OrdinalIgnoreCase));
            var assemblyVersion = new Version(DeceiveVersion.Replace("v", "", StringComparison.OrdinalIgnoreCase));
            // Earlier = -1, Same = 0, Later = 1
            if (assemblyVersion.CompareTo(githubVersion) != -1)
                return null;

            // Check if we have shown this before.
            var latestShownVersion = Persistence.GetPromptedUpdateVersion();

            // If we have, return.
            if (string.IsNullOrEmpty(latestShownVersion) && latestShownVersion == latestVersion)
                return null;

            // Show a message and record the latest shown.
            Persistence.SetPromptedUpdateVersion(latestVersion);

            return release?["html_url"]?.ToString();
        }
        catch (HttpRequestException ex)
        {
            // Log the exception or handle it as needed.
            Console.WriteLine($"Request error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            // Log the exception or handle it as needed.
            Console.WriteLine($"JSON error: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            // Log the exception or handle it as needed.
            Console.WriteLine($"Invalid operation error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            // Log the exception or handle it as needed.
            Console.WriteLine($"Task canceled error: {ex.Message}");
        }
        catch (IOException ex)
        {
            // Log the exception or handle it as needed.
            Console.WriteLine($"IO error: {ex.Message}");
        }
        return null;
    }

    private static List<Process> GetProcesses()
    {
        var riotCandidates = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Where(process => process.Id != Environment.ProcessId).ToList();
        riotCandidates.AddRange(Process.GetProcessesByName("LeagueClient"));
        riotCandidates.AddRange(Process.GetProcessesByName("LoR"));
        riotCandidates.AddRange(Process.GetProcessesByName("VALORANT-Win64-Shipping"));
        riotCandidates.AddRange(Process.GetProcessesByName("RiotClientServices"));
        riotCandidates.AddRange(Process.GetProcessesByName("Riot Client"));
        return riotCandidates;
    }

    // Return the currently running Riot Client process, or null if none are running.
    public static Process? GetRiotClientProcess() => Process.GetProcessesByName("RiotClientServices").FirstOrDefault();

    // Checks if there is a running LCU/LoR/VALORANT/RC or Deceive instance.
    public static bool IsClientRunning() => GetProcesses().Count != 0;

    // Kills the running LCU/LoR/VALORANT/RC or Deceive instance, if applicable.
    public static async Task KillProcesses()
    {
        try
        {
            foreach (var process in GetProcesses())
            {
                process.Refresh();
                if (process.HasExited)
                    continue;
                process.Kill();
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (Win32Exception ex)
        {
            // thank you C# and your horrible win32 ecosystem integration, I have no clue if this is correct
            if (ex.NativeErrorCode == -2147467259 || ex.ErrorCode == -2147467259 || ex.ErrorCode == 5 || ex.NativeErrorCode == 5)
            {
                var box = MessageBoxManager.GetMessageBoxStandard(
                    StartupHandler.DeceiveTitle,
                    "Deceive could not stop existing Riot processes because \n" +
                    "it does not have the right permissions. \n" +
                    "Please relaunch this application as an administrator and try again.",
                    ButtonEnum.Ok,
                    Icon.Error
                );
                await box.ShowAsync().ConfigureAwait(false);
                Environment.Exit(0);
            }

            throw;
        }
    }

    // Checks for any installed Riot Client configuration,
    // and returns the path of the client if it does. Else, returns null.
    public static string? GetRiotClientPath()
    {
        // Find the RiotClientInstalls file.
        var installPath = Path.Combine(
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "/Users/Shared"
                : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Riot Games/RiotClientInstalls.json"
        );
        if (!File.Exists(installPath))
            return null;

        try
        {
            // occasionally this deserialization may error, because the RC occasionally corrupts its own
            // configuration file (wtf riot?). we will return null in that case, which will cause a prompt
            // telling the user to launch a game normally once
            var data = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(installPath));
            var rcPaths = new List<string?> { data?["rc_default"]?.ToString(), data?["rc_live"]?.ToString(), data?["rc_beta"]?.ToString() };

            return rcPaths.FirstOrDefault(File.Exists);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
