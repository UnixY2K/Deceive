using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Deceive.Controllers;
using Application = Avalonia.Application;

namespace Deceive;

internal static class StartupHandler
{
    public static string DeceiveTitle => "Deceive " + Utils.DeceiveVersion;

    // Arguments are parsed through System.CommandLine.DragonFruit.
    /// <param name="args">The game to be launched, or automatically determined if not passed.</param>
    /// <param name="gamePatchline">The patchline to be used for launching the game.</param>
    /// <param name="riotClientParams">Any extra parameters to be passed to the Riot Client.</param>
    /// <param name="gameParams">Any extra parameters to be passed to the launched game.</param>
    [STAThread]
    public static async Task Main(LaunchGame args = LaunchGame.Auto, string gamePatchline = "live", string? riotClientParams = null, string? gameParams = null)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

        Arguments.game = args;
        Arguments.gamePatchline = gamePatchline;
        Arguments.riotClientParams = riotClientParams;
        Arguments.gameParams = gameParams;
        await StartDeceiveAsync().ConfigureAwait(false);
    }

    /// Actual main function. Wrapped into a separate function so we can catch exceptions.
    private static async Task StartDeceiveAsync()
    {

        try
        {
            await File.WriteAllTextAsync(Path.Combine(Persistence.DataDir, "debug.log"), string.Empty).ConfigureAwait(false);
            Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(Persistence.DataDir, "debug.log")));
            Debug.AutoFlush = true;
            Trace.WriteLine(DeceiveTitle);
        }
        catch (IOException ex)
        {
            Trace.WriteLine($"Failed to write to debug log: {ex.Message}");
        }

        var Application = BuildAvaloniaApp();
        Application.StartWithClassicDesktopLifetime([]);

    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Log all unhandled exceptions
        Trace.WriteLine(e.ExceptionObject as Exception);
        Trace.WriteLine(Environment.StackTrace);
    }



    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
