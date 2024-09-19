
using System;
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
                        Utils.KillProcesses();
                        await Task.Delay(2000).ConfigureAwait(true); // Riot Client takes a while to die
                    }
                }

            }
            return true;
        }
    }
}
