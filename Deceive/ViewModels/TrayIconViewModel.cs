using CommunityToolkit.Mvvm.ComponentModel;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Deceive.ViewModels;

public partial class TrayIconViewModel : ViewModelBase, INotifyPropertyChanged
{

    public string Status { get; set; } = null!;
    private string StatusFile { get; } = Path.Combine(Persistence.DataDir, "status");
    public async Task QuitCommand()
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            StartupHandler.DeceiveTitle,
            "Are you sure you want to stop Deceive? This will also stop related games if they are running.",
            ButtonEnum.YesNo,
            Icon.Question
        );
        var result = await box.ShowAsync().ConfigureAwait(false);
        if (result == ButtonResult.Yes)
        {
            await Utils.KillProcesses().ConfigureAwait(false);
            await Task.Delay(2000).ConfigureAwait(false);
            SaveStatus();
            Environment.Exit(0);
        }
    }

    public async Task RestartCommand()
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            StartupHandler.DeceiveTitle,
            "Restart Deceive to launch a different game? This will also stop related games if they are running.",
            ButtonEnum.YesNo,
            Icon.Question
        );
        var result = await box.ShowAsync().ConfigureAwait(false);
        if (result == ButtonResult.Yes)
        {
            await Utils.KillProcesses().ConfigureAwait(false);
            await Task.Delay(2000).ConfigureAwait(false);
            SaveStatus();
            Persistence.SetDefaultLaunchGame(LaunchGame.Prompt);
            Process.Start(Application.ExecutablePath);
            Environment.Exit(0);
        }
    }

    private void SaveStatus() => File.WriteAllText(StatusFile, Status);

}
