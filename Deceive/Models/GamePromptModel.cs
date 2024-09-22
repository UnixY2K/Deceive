
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Deceive.Controllers;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Deceive.Models
{
    internal static class GamePromptModel
    {

        public static async Task<bool> CheckClientRunning()
        {
            // Refuse to do anything if the client is already running, unless we're specifically
            // allowing that through League/RC's --allow-multiple-clients.
            if (!(Arguments.riotClientParams?.Contains("allow-multiple-clients", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                if (Utils.IsClientRunning())
                {
                    var box = MessageBoxManager.GetMessageBoxStandard(StartupHandler.DeceiveTitle,
                        "The Riot Client is currently running. In order to mask your online status, the Riot Client needs to be started by Deceive. " +
                        "Do you want Deceive to stop the Riot Client and games launched by it, so that it can restart with the proper configuration?",
                        ButtonEnum.YesNo,
                        Icon.Question
                    );
                    var result = await box.ShowAsync().ConfigureAwait(true);
                    if (result is not ButtonResult.Yes)
                        return false;
                    while (Utils.IsClientRunning())
                    {
                        await Utils.KillProcesses().ConfigureAwait(true);
                        await Task.Delay(2000).ConfigureAwait(true); // Riot Client takes a while to die
                    }
                }

            }
            return true;
        }

        public static async Task CheckForUpdates()
        {
            string? updateUrl = await Utils.CheckForUpdatesAsync().ConfigureAwait(true);
            if (updateUrl is not null)
            {
                var box = MessageBoxManager.GetMessageBoxStandard(
                    StartupHandler.DeceiveTitle,
                    $"There is a new version of Deceive available. You are currently using Deceive {Utils.DeceiveVersion}. " +
                    $"Do you want to download the new version?",
                    ButtonEnum.YesNo,
                    Icon.Question
                );
                var result = await box.ShowAsync().ConfigureAwait(true);
                if (result is ButtonResult.Yes)
                    Process.Start(updateUrl);
            }
        }

        public static async Task<bool> CheckRiotClientPath()
        {
            var riotClientPath = Utils.GetRiotClientPath();
            if (riotClientPath is null)
            {
                var box = MessageBoxManager.GetMessageBoxStandard(
                    StartupHandler.DeceiveTitle,
                    "Deceive was unable to find the path to the Riot Client. Usually this can be resolved by launching any Riot Games game once, then launching Deceive again. " +
                    "If this does not resolve the issue, please file a bug report through GitHub (https://github.com/molenzwiebel/Deceive) or Discord.",
                    ButtonEnum.Ok,
                    Icon.Error
                );
                await box.ShowAsync().ConfigureAwait(true);
                return false;
            }
            return true;
        }
    }
}
