using Avalonia.Controls;
using Avalonia.Interactivity;
using Deceive.ViewModels;
using System;
using Button = Avalonia.Controls.Button;

namespace Deceive.Views;

public partial class GamePromptWindow : Window
{

    internal static LaunchGame SelectedGame = LaunchGame.Auto;

    private bool gameSelected;
    public GamePromptWindow()
    {
        InitializeComponent();
        Closing += (sender, e) => OnClose(sender, e);
        Loaded += (sender, e) => OnLoaded(sender, e);
    }


    private void OnClose(object? sender, WindowClosingEventArgs e)
    {

    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // check if the riot client is running

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
        gameSelected = true;
        Close();
    }
}