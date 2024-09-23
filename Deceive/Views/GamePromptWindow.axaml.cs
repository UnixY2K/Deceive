using Avalonia.Controls;
using Avalonia.Interactivity;
using Deceive.Controllers;
using Deceive.Models;
using Deceive.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Deceive.Views;

public partial class GamePromptWindow : Window
{

    internal static LaunchGame SelectedGame = LaunchGame.Auto;

    private TcpListener? _listener;
    private int port;

    public GamePromptWindow()
    {
        InitializeComponent();
        SetupEvents();
        SetupListener();
    }

    private void SetupEvents()
    {
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
            await GamePromptModel.CheckForUpdates().ConfigureAwait(true);
            if (!closedClient)
            {
                Close();
                return;
            }
            if (!await GamePromptModel.CheckRiotClientPath().ConfigureAwait(true))
            {
                Close();
                return;
            }
            var game = Arguments.game;
            if (game is LaunchGame.Auto)
            {
                game = Persistence.GetDefaultLaunchGame();
                if (game is not LaunchGame.Prompt)
                {
                    HandleLaunchChoiceAsync(game);
                    return;
                }
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
        Hide();
        StartGame();
    }

    private void StartGame()
    {
        var game = Persistence.SelectedGame;
        var launchProduct = game switch
        {
            LaunchGame.LoL => "league_of_legends",
            LaunchGame.LoR => "bacon",
            LaunchGame.VALORANT => "valorant",
            LaunchGame.RiotClient => null,
            var x => throw new InvalidOperationException("Unexpected LaunchGame: " + x)
        };

        var proxyServer = new ConfigProxy(port);
        var riotClientPath = Utils.GetRiotClientPath();
        var startArgs = new ProcessStartInfo { FileName = riotClientPath, Arguments = $"--client-config-url=\"http://127.0.0.1:{proxyServer.ConfigPort}\"" };
        if (launchProduct is not null)
            startArgs.Arguments += $" --launch-product={launchProduct} --launch-patchline={Arguments.gamePatchline}";

        if (Arguments.riotClientParams is not null)
            startArgs.Arguments += $" {Arguments.riotClientParams}";

        if (Arguments.gameParams is not null)
            startArgs.Arguments += $" -- {Arguments.gameParams}";

        Trace.WriteLine($"About to launch Riot Client with parameters:\n{startArgs.Arguments}");
        var riotClient = Process.Start(startArgs);
        // Kill Deceive when Riot Client has exited, so no ghost Deceive exists.
        if (riotClient is not null)
        {
            ListenToRiotClientExit(riotClient);
        }

        _ = MainController.Instance;
        var servingClients = false;
        proxyServer.PatchedChatServer += (_, args) =>
        {
            Trace.WriteLine($"The original chat server details were {args.ChatHost}:{args.ChatPort}");

            // Step 6: Start serving incoming connections and proxy them!
            if (servingClients)
                return;
            servingClients = true;
            MainController.Instance.StartServingClients(_listener!, args.ChatHost ?? "", args.ChatPort);
        };
    }
    private void SetupListener()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Trace.WriteLine($"Chat proxy listening on port {port}");
    }

    private static void ListenToRiotClientExit(Process riotClientProcess)
    {
        riotClientProcess.EnableRaisingEvents = true;
        riotClientProcess.Exited += async (sender, e) =>
        {
            Trace.WriteLine("Detected Riot Client exit.");
            await Task.Delay(3000).ConfigureAwait(false); // wait for a bit to ensure this is not a relaunch triggered by the RC

            var newProcess = Utils.GetRiotClientProcess();
            if (newProcess is not null)
            {
                Trace.WriteLine("A new Riot Client process spawned, monitoring that for exits.");
                ListenToRiotClientExit(newProcess);
            }
            else
            {
                Trace.WriteLine("No new clients spawned after waiting, killing ourselves.");
                Environment.Exit(0);
            }
        };
    }

}