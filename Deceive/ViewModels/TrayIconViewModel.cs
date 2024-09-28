using CommunityToolkit.Mvvm.ComponentModel;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Deceive.ViewModels;

public partial class TrayIconViewModel : ViewModelBase, INotifyPropertyChanged
{

#pragma warning disable CA1822 // Marcar miembros como static
    public async Task QuitCommand()
#pragma warning restore CA1822 // Marcar miembros como static
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            StartupHandler.DeceiveTitle,
            "Are you sure you want to stop Deceive? \n" + 
            "This will also stop related games if they are running.",
            ButtonEnum.YesNo,
            Icon.Question
        );
        var result = await box.ShowAsync().ConfigureAwait(false);
        if (result == ButtonResult.Yes)
        {
            await Utils.KillProcesses().ConfigureAwait(false);
            await Task.Delay(2000).ConfigureAwait(false);
            MainController.Instance.SaveStatus();
            Environment.Exit(0);
        }
    }

#pragma warning disable CA1822 // Marcar miembros como static
    public async Task RestartCommand()
#pragma warning restore CA1822 // Marcar miembros como static
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            StartupHandler.DeceiveTitle,
            "Restart Deceive to launch a different game? \n " + 
            "This will also stop related games if they are running.",
            ButtonEnum.YesNo,
            Icon.Question
        );
        var result = await box.ShowAsync().ConfigureAwait(false);
        if (result == ButtonResult.Yes)
        {
            await Utils.KillProcesses().ConfigureAwait(false);
            await Task.Delay(2000).ConfigureAwait(false);
            MainController.Instance.SaveStatus();
            Persistence.SetDefaultLaunchGame(LaunchGame.Prompt);
            Process.Start(Environment.ProcessPath ?? "");
            Environment.Exit(0);
        }
    }


    public void ToggleEnabledCommand()
    {
        Enabled = !Enabled;
        MainController.Instance.SetEnabled(Enabled);
    }

    public void ToggleEnabledLobbyChatCommand()
    {
        EnabledLobbyChat = !EnabledLobbyChat;
        MainController.Instance.SetEnabledLobbyChat(EnabledLobbyChat);
    }

    public void SetOnlineStatusCommand()
    {
        if (!Enabled)
        {
            ToggleEnabledCommand();
        }
        MainController.Instance.SetOnlineStatus();
    }

    public void SetOfflineStatusCommand()
    {
        if (!Enabled)
        {
            ToggleEnabledCommand();
        }
        MainController.Instance.SetOfflineStatus();
    }

    public void SetMobileStatusCommand()
    {
        if (!Enabled)
        {
            ToggleEnabledCommand();
        }
        MainController.Instance.SetMobileStatus();
    }

#pragma warning disable CA1822 // Marcar miembros como static
    public void SendMessageCommand()
#pragma warning restore CA1822 // Marcar miembros como static
    {
        MainController.Instance.SendTestMessage();
    }


    [ObservableProperty]
    private bool enabled = true;
    [ObservableProperty]
    private bool enabledLobbyChat = true;
    [ObservableProperty]
    private bool isOnline = false;
    [ObservableProperty]
    private bool isOffline = true;
    [ObservableProperty]
    private bool isMobile = false;

}
