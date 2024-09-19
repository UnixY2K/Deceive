using Avalonia.Controls;
using Avalonia.Interactivity;
using Deceive.Models;
using Deceive.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Deceive.Views;

public partial class GamePromptWindow : Window
{

    internal static LaunchGame SelectedGame = LaunchGame.Auto;

    public GamePromptWindow()
    {
        InitializeComponent();
        Closing += (sender, e) => OnClose(sender, e);
        Loaded += async (sender, e) => await OnLoadedAsync(sender, e).ConfigureAwait(true);
    }


    private void OnClose(object? sender, WindowClosingEventArgs e)
    {

    }

    private async Task OnLoadedAsync(object? sender, RoutedEventArgs e)
    {
        Hide();
        // check if the riot client is running
        try
        {
            var closedClient = await GamePromptModel.CheckClientRunning().ConfigureAwait(true);
            if (!closedClient)
            {
                Close();
                return;
            }
            Show();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
            // Show some kind of message so that Deceive doesn't just disappear.
            var box = MessageBoxManager.GetMessageBoxStandard(StartupHandler.DeceiveTitle,
                "Deceive encountered an error and couldn't properly initialize itself. " +
                "Please contact the creator through GitHub (https://github.com/molenzwiebel/Deceive) or Discord.\n\n" + ex,
                ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error
            );
            await box.ShowAsync().ConfigureAwait(true);
            Close();
            throw;
        }
    }



    private void GameClickHandler(object sender, RoutedEventArgs e)
    {
        var button = (sender as Button);
        var game = button?.Name switch
        {
            "LoL" => LaunchGame.LoL,
            "LoR" => LaunchGame.LoR,
            "Valorant" => LaunchGame.VALORANT,
            "RiotClient" => LaunchGame.RiotClient,
            _ => throw new InvalidOperationException(message: $"Unexpected button name: {button?.Name}")
        };
        HandleLaunchChoiceAsync(game);
    }


    private void HandleLaunchChoiceAsync(LaunchGame game)
    {
        // get view model
        var viewModel = DataContext as GamePromptWindowViewModel;
        if (viewModel?.RememberDesition ?? false)
            Persistence.SetDefaultLaunchGame(game);

        Persistence.SetSelectedGame(game);

        SelectedGame = game;
        Close();
    }
}