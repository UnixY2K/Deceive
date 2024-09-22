using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Deceive.Properties;

namespace Deceive;

internal sealed class MainController : ApplicationContext
{
    internal MainController()
    {
        TrayIcon = new NotifyIcon
        {
            Icon = Resources.DeceiveIcon,
            Visible = true,
            BalloonTipTitle = StartupHandler.DeceiveTitle,
            BalloonTipText = "Deceive is currently masking your status. Right-click the tray icon for more options."
        };
        TrayIcon.ShowBalloonTip(5000);

        LoadStatus();
        UpdateTray();
    }

    private NotifyIcon TrayIcon { get; }
    public bool Enabled { get; set; } = true;
    public string Status { get; set; } = null!;
    private string StatusFile { get; } = Path.Combine(Persistence.DataDir, "status");
    public bool ConnectToMuc { get; set; } = true;
    private bool SentIntroductionText { get; set; }
    private CancellationTokenSource? ShutdownToken { get; set; }

    private ToolStripMenuItem EnabledMenuItem { get; set; } = null!;
    private ToolStripMenuItem ChatStatus { get; set; } = null!;
    private ToolStripMenuItem OfflineStatus { get; set; } = null!;
    private ToolStripMenuItem MobileStatus { get; set; } = null!;

    private List<ProxiedConnection> Connections { get; } = [];

    public void StartServingClients(TcpListener server, string chatHost, int chatPort)
    {
        Task.Run(() => ServeClientsAsync(server, chatHost, chatPort));
    }

    private async Task ServeClientsAsync(TcpListener server, string chatHost, int chatPort)
    {
        using var cert = new X509Certificate2(Resources.Certificate);

        while (true)
        {
            try
            {
                // no need to shutdown, we received a new request
                ShutdownToken?.Cancel();
                ShutdownToken = null;

                var incoming = await server.AcceptTcpClientAsync().ConfigureAwait(false);
                var sslIncoming = new SslStream(incoming.GetStream());
                await sslIncoming.AuthenticateAsServerAsync(cert).ConfigureAwait(false);

                TcpClient outgoing;
                while (true)
                {
                    try
                    {
                        outgoing = new TcpClient(chatHost, chatPort);
                        break;
                    }
                    catch (SocketException e)
                    {
                        Trace.WriteLine(e);
                        var result = MessageBox.Show(
                            "Unable to connect to the chat server. Please check your internet connection. " +
                            "If this issue persists and you can connect to chat normally without Deceive, " +
                            "please file a bug report through GitHub (https://github.com/molenzwiebel/Deceive) or Discord.",
                            StartupHandler.DeceiveTitle,
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Error,
                            MessageBoxDefaultButton.Button1
                        );
                        if (result == DialogResult.Cancel)
                            Environment.Exit(0);
                    }
                }

                var sslOutgoing = new SslStream(outgoing.GetStream());
                await sslOutgoing.AuthenticateAsClientAsync(chatHost).ConfigureAwait(false);

                var proxiedConnection = new ProxiedConnection(this, sslIncoming, sslOutgoing);
                proxiedConnection.Start();
                proxiedConnection.ConnectionErrored += (_, _) =>
                {
                    Trace.WriteLine("Disconnected incoming connection.");
                    Connections.Remove(proxiedConnection);

                    if (Connections.Count == 0)
                    {
                        Task.Run(ShutdownIfNoReconnect);
                    }
                };
                Connections.Add(proxiedConnection);

                if (!SentIntroductionText)
                {
                    SentIntroductionText = true;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(10_000).ConfigureAwait(false);
                        await SendIntroductionTextAsync().ConfigureAwait(false);
                    });
                }
            } catch (Exception e)
            {
                Trace.WriteLine("Failed to handle incoming connection.");
                Trace.WriteLine(e);
            }
        }
    }

    private void UpdateTray()
    {
        var aboutMenuItem = new ToolStripMenuItem(StartupHandler.DeceiveTitle) { Enabled = false };

        EnabledMenuItem = new ToolStripMenuItem("Enabled", null, async (_, _) =>
        {
            Enabled = !Enabled;
            await UpdateStatusAsync(Enabled ? Status : "chat").ConfigureAwait(false);
            await SendMessageFromFakePlayerAsync(Enabled ? "Deceive is now enabled." : "Deceive is now disabled.").ConfigureAwait(false);
            UpdateTray();
        })
        { Checked = Enabled };

        var mucMenuItem = new ToolStripMenuItem("Enable lobby chat", null, (_, _) =>
        {
            ConnectToMuc = !ConnectToMuc;
            UpdateTray();
        })
        { Checked = ConnectToMuc };

        ChatStatus = new ToolStripMenuItem("Online", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "chat").ConfigureAwait(false);
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("chat") };

        OfflineStatus = new ToolStripMenuItem("Offline", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "offline").ConfigureAwait(false);
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("offline") };

        MobileStatus = new ToolStripMenuItem("Mobile", null, async (_, _) =>
        {
            await UpdateStatusAsync(Status = "mobile").ConfigureAwait(false);
            Enabled = true;
            UpdateTray();
        })
        { Checked = Status.Equals("mobile") };

        var typeMenuItem = new ToolStripMenuItem("Status Type", null, ChatStatus, OfflineStatus, MobileStatus);


        TrayIcon.ContextMenuStrip = new ContextMenuStrip();

#if DEBUG
        var sendTestMsg = new ToolStripMenuItem("Send message", null, async (_, _) => { await SendMessageFromFakePlayerAsync("Test").ConfigureAwait(false); });

        TrayIcon.ContextMenuStrip.Items.AddRange(
        [
            aboutMenuItem, EnabledMenuItem, typeMenuItem, mucMenuItem, sendTestMsg
        ]);
#else
        TrayIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[] { aboutMenuItem, EnabledMenuItem, typeMenuItem, mucMenuItem, restartWithDifferentGameItem, quitMenuItem });
#endif
    }

    public async Task HandleChatMessage(string content)
    {
        if (content.Contains("offline", StringComparison.CurrentCultureIgnoreCase))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is now enabled.").ConfigureAwait(false);
            OfflineStatus.PerformClick();
        }
        else if (content.Contains("mobile", StringComparison.CurrentCultureIgnoreCase))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is now enabled.").ConfigureAwait(false);
            MobileStatus.PerformClick();
        }
        else if (content.ToLower().Contains("online"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is now enabled.").ConfigureAwait(false);
            ChatStatus.PerformClick();
        }
        else if (content.ToLower().Contains("enable"))
        {
            if (Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is already enabled.").ConfigureAwait(false);
            else
                EnabledMenuItem.PerformClick();
        }
        else if (content.ToLower().Contains("disable"))
        {
            if (!Enabled)
                await SendMessageFromFakePlayerAsync("Deceive is already disabled.").ConfigureAwait(false);
            else
                EnabledMenuItem.PerformClick();
        }
        else if (content.ToLower().Contains("status"))
        {
            if (Status == "chat")
                await SendMessageFromFakePlayerAsync("You are appearing online.").ConfigureAwait(false);
            else
                await SendMessageFromFakePlayerAsync("You are appearing " + Status + ".").ConfigureAwait(false);
        }
        else if (content.ToLower().Contains("help"))
        {
            await SendMessageFromFakePlayerAsync("You can send the following messages to quickly change Deceive settings: online/offline/mobile/enable/disable/status").ConfigureAwait(false);
        }
    }

    private async Task SendIntroductionTextAsync()
    {
        SentIntroductionText = true;
        await SendMessageFromFakePlayerAsync("Welcome! Deceive is running and you are currently appearing " + Status +
                                             ". Despite what the game client may indicate, you are appearing offline to your friends unless you manually disable Deceive.").ConfigureAwait(false);
        await Task.Delay(200).ConfigureAwait(false);
        await SendMessageFromFakePlayerAsync(
            "If you want to invite others while being offline, you may need to disable Deceive for them to accept. You can enable Deceive again as soon as they are in your lobby.").ConfigureAwait(false);
        await Task.Delay(200).ConfigureAwait(false);
        await SendMessageFromFakePlayerAsync("To enable or disable Deceive, or to configure other settings, find Deceive in your tray icons.").ConfigureAwait(false);
        await Task.Delay(200).ConfigureAwait(false);
        await SendMessageFromFakePlayerAsync("Have fun!").ConfigureAwait(false);
    }

    private async Task SendMessageFromFakePlayerAsync(string message)
    {
        foreach (var connection in Connections)
            await connection.SendMessageFromFakePlayerAsync(message).ConfigureAwait(false);
    }

    private async Task UpdateStatusAsync(string newStatus)
    {
        foreach (var connection in Connections)
            await connection.UpdateStatusAsync(newStatus).ConfigureAwait(false);

        if (newStatus == "chat")
            await SendMessageFromFakePlayerAsync("You are now appearing online.").ConfigureAwait(false);
        else
            await SendMessageFromFakePlayerAsync("You are now appearing " + newStatus + ".").ConfigureAwait(false);
    }

    private void LoadStatus()
    {
        if (File.Exists(StatusFile))
            Status = File.ReadAllText(StatusFile) == "mobile" ? "mobile" : "offline";
        else
            Status = "offline";
    }

    private async Task ShutdownIfNoReconnect()
    {
        ShutdownToken ??= new CancellationTokenSource();
        await Task.Delay(60_000, ShutdownToken.Token).ConfigureAwait(false);

        Trace.WriteLine("Received no new connections after 60s, shutting down.");
        Environment.Exit(0);
    }

    private void SaveStatus() => File.WriteAllText(StatusFile, Status);
}
