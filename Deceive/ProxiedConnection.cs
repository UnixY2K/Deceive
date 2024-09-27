using System;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Deceive;

internal sealed class ProxiedConnection
{
    private MainController MainController { get; set; }

    private SslStream Incoming { get; set; } = null!;
    private SslStream Outgoing { get; set; } = null!;
    private bool Connected { get; set; } = true;
    private string LastPresence { get; set; } = null!; // we resend this if the state changes
    private bool InsertedFakePlayer { get; set; }
    private bool SentFakePlayerPresence { get; set; }
    private string? ValorantVersion { get; set; }

    internal event EventHandler? ConnectionErrored;

    internal ProxiedConnection(MainController main, SslStream incoming, SslStream outgoing)
    {
        MainController = main;
        Incoming = incoming;
        Outgoing = outgoing;
    }

    public void Start()
    {
        Task.Run(IncomingLoopAsync);
        Task.Run(OutgoingLoopAsync);
    }

    private async Task IncomingLoopAsync()
    {
        try
        {
            int byteCount;
            var bytes = new byte[8192];

            do
            {
                byteCount = await Incoming.ReadAsync(bytes.AsMemory(0, bytes.Length)).ConfigureAwait(false);

                var content = Encoding.UTF8.GetString(bytes, 0, byteCount);

                // If this is possibly a presence stanza, rewrite it.
                if (content.Contains("<presence", StringComparison.InvariantCultureIgnoreCase) && MainController.Enabled)
                {
                    Trace.WriteLine("<!--RC TO SERVER ORIGINAL-->" + content);
                    await PossiblyRewriteAndResendPresenceAsync(content, MainController.Status).ConfigureAwait(false);
                }
                else if (content.Contains("41c322a1-b328-495b-a004-5ccd3e45eae8@eu1.pvp.net", StringComparison.InvariantCultureIgnoreCase))
                {
                    await MainController.HandleChatMessage(content).ConfigureAwait(false);

                    //Don't send anything involving our fake user to chat servers
                    Trace.WriteLine("<!--RC TO SERVER REMOVED-->" + content);
                }
                else
                {
                    await Outgoing.WriteAsync(bytes.AsMemory(0, byteCount)).ConfigureAwait(false);
                    Trace.WriteLine("<!--RC TO SERVER-->" + content);
                }

                if (InsertedFakePlayer && !SentFakePlayerPresence)
                    await SendFakePlayerPresenceAsync().ConfigureAwait(false);
            } while (byteCount != 0 && Connected);
        }
        catch (IOException e)
        {
            Trace.WriteLine("Incoming IO errored.");
            Trace.WriteLine(e);
        }
        catch (ObjectDisposedException e)
        {
            Trace.WriteLine("Incoming object disposed errored.");
            Trace.WriteLine(e);
        }
        catch (Exception e)
        {
            Trace.WriteLine("Incoming errored.");
            Trace.WriteLine(e);
            throw;
        }
        finally
        {
            Trace.WriteLine("Incoming closed.");
            OnConnectionErrored();
        }
    }

    private async Task OutgoingLoopAsync()
    {
        try
        {
            int byteCount;
            var bytes = new byte[8192];

            do
            {
                byteCount = await Outgoing.ReadAsync(bytes.AsMemory(0, bytes.Length)).ConfigureAwait(false);
                var content = Encoding.UTF8.GetString(bytes, 0, byteCount);

                // Insert fake player into roster
                const string roster = "<query xmlns='jabber:iq:riotgames:roster'>";
                if (!InsertedFakePlayer && content.Contains(roster, StringComparison.InvariantCultureIgnoreCase))
                {
                    InsertedFakePlayer = true;
                    Trace.WriteLine("<!--SERVER TO RC ORIGINAL-->" + content);
                    content = content.Insert(content.IndexOf(roster, StringComparison.Ordinal) + roster.Length,
                        "<item jid='41c322a1-b328-495b-a004-5ccd3e45eae8@eu1.pvp.net' name='&#9;Deceive Active!' subscription='both' puuid='41c322a1-b328-495b-a004-5ccd3e45eae8'>" +
                        "<group priority='9999'>Deceive</group>" +
                        "<state>online</state>" +
                        "<id name='&#9;Deceive Active!' tagline='...'/>" +
                        "<lol name='&#9;Deceive Active!'/>" +
                        "<platforms><riot name='&#9;Deceive Active' tagline='...'/></platforms>" +
                        "</item>");
                    var contentBytes = Encoding.UTF8.GetBytes(content);
                    await Incoming.WriteAsync(contentBytes).ConfigureAwait(false);
                    Trace.WriteLine("<!--DECEIVE TO RC-->" + content);
                }
                else
                {
                    await Incoming.WriteAsync(bytes.AsMemory(0, byteCount)).ConfigureAwait(false);
                    Trace.WriteLine("<!--SERVER TO RC-->" + content);
                }
            } while (byteCount != 0 && Connected);
        }
        catch (IOException e)
        {
            Trace.WriteLine("Outgoing IO errored.");
            Trace.WriteLine(e);
        }
        catch (ObjectDisposedException e)
        {
            Trace.WriteLine("Outgoing object disposed errored.");
            Trace.WriteLine(e);
        }
        catch (Exception e)
        {
            Trace.WriteLine("Outgoing errored.");
            Trace.WriteLine(e);
            throw;
        }
        finally
        {
            Trace.WriteLine("Outgoing closed.");
            OnConnectionErrored();
        }
    }

    private async Task PossiblyRewriteAndResendPresenceAsync(string content, string targetStatus)
    {
        try
        {
            LastPresence = content;
            var wrappedContent = "<xml>" + content + "</xml>";
            var xml = XDocument.Load(new StringReader(wrappedContent));

            if (xml.Root is null)
                return;
            if (xml.Root.HasElements is false)
                return;

            foreach (var presence in xml.Root.Elements())
            {
                if (presence.Name != "presence")
                    continue;
                if (presence.Attribute("to") is not null)
                {
                    if (MainController.ConnectToMuc)
                        continue;
                    presence.Remove();
                }

                if (targetStatus != "chat" || presence.Element("games")?.Element("league_of_legends")?.Element("st")?.Value != "dnd")
                {
                    presence.Element("show")?.ReplaceNodes(targetStatus);
                    presence.Element("games")?.Element("league_of_legends")?.Element("st")?.ReplaceNodes(targetStatus);
                }

                if (targetStatus == "chat")
                    continue;
                presence.Element("status")?.Remove();

                if (targetStatus == "mobile")
                {
                    presence.Element("games")?.Element("league_of_legends")?.Element("p")?.Remove();
                    presence.Element("games")?.Element("league_of_legends")?.Element("m")?.Remove();
                }
                else
                {
                    presence.Element("games")?.Element("league_of_legends")?.Remove();
                }

                // Remove Legends of Runeterra presence
                presence.Element("games")?.Element("bacon")?.Remove();

                // Extracts current VALORANT from the user's own presence, so that we can show a fake
                // player with the proper version and avoid "Version Mismatch" from being shown.
                //
                // This isn't technically necessary, but people keep coming in and asking whether
                // the scary red text means Deceive doesn't work, so might as well do this and
                // get a slightly better user experience.
                if (ValorantVersion is null)
                {
                    var valorantBase64 = presence.Element("games")?.Element("valorant")?.Element("p")?.Value;
                    if (valorantBase64 is not null)
                    {
                        var valorantPresence = Encoding.UTF8.GetString(Convert.FromBase64String(valorantBase64));
                        var valorantJson = JsonSerializer.Deserialize<JsonNode>(valorantPresence);
                        ValorantVersion = valorantJson?["partyClientVersion"]?.GetValue<string>();
                        Trace.WriteLine("Found VALORANT version: " + ValorantVersion);
                        // only resend
                        if (InsertedFakePlayer && ValorantVersion is not null)
                            await SendFakePlayerPresenceAsync().ConfigureAwait(false);
                    }
                }

                // Remove VALORANT presence
                presence.Element("games")?.Element("valorant")?.Remove();
            }

            var sb = new StringBuilder();
            var xws = new XmlWriterSettings { OmitXmlDeclaration = true, Encoding = Encoding.UTF8, ConformanceLevel = ConformanceLevel.Fragment, Async = true };
            using (var xw = XmlWriter.Create(sb, xws))
            {
                foreach (var xElement in xml.Root.Elements())
                    xElement.WriteTo(xw);
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            await Outgoing.WriteAsync(bytes).ConfigureAwait(false);
            Trace.WriteLine("<!--DECEIVE TO SERVER-->" + sb);
        }
        catch (XmlException e)
        {
            Trace.WriteLine(e);
            Trace.WriteLine("XML Error rewriting presence.");
            throw;
        }
        catch (JsonException e)
        {
            Trace.WriteLine(e);
            Trace.WriteLine("JSON Error rewriting presence.");
            throw;
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            Trace.WriteLine("Unexpected error rewriting presence.");
            throw;
        }
    }

    private async Task SendFakePlayerPresenceAsync()
    {
        SentFakePlayerPresence = true;
        // VALORANT requires a recent version to not display "Version Mismatch"
        var valorantPresence = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{{\"isValid\":true,\"partyId\":\"00000000-0000-0000-0000-000000000000\",\"partyClientVersion\":\"{ValorantVersion ?? "unknown"}\",\"accountLevel\":1000}}")
        );

        var randomStanzaId = Guid.NewGuid();
        var unixTimeMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var presenceMessage =
            $"<presence from='41c322a1-b328-495b-a004-5ccd3e45eae8@eu1.pvp.net/RC-Deceive' id='b-{randomStanzaId}'>" +
            "<games>" +
            $"<keystone><st>chat</st><s.t>{unixTimeMilliseconds}</s.t><s.p>keystone</s.p><pty/></keystone>" +
            $"<league_of_legends><st>chat</st><s.t>{unixTimeMilliseconds}</s.t><s.p>league_of_legends</s.p><s.c>live</s.c><p>{{&quot;pty&quot;:true}}</p></league_of_legends>" + // No Region s.r keeps it in the main "League" category rather than "Other Servers" in every region with "Group Games & Servers" active
            $"<valorant><st>chat</st><s.t>{unixTimeMilliseconds}</s.t><s.p>valorant</s.p><s.r>PC</s.r><p>{valorantPresence}</p><pty/></valorant>" +
            $"<bacon><st>chat</st><s.t>{unixTimeMilliseconds}</s.t><s.l>bacon_availability_online</s.l><s.p>bacon</s.p></bacon>" +
            "</games>" +
            "<show>chat</show>" +
            "<platform>riot</platform>" +
            "<status/>" +
            "</presence>";

        var bytes = Encoding.UTF8.GetBytes(presenceMessage);
        await Incoming.WriteAsync(bytes).ConfigureAwait(false);
        Trace.WriteLine("<!--DECEIVE TO RC-->" + presenceMessage);
    }

    public async Task SendMessageFromFakePlayerAsync(string message)
    {
        if (!InsertedFakePlayer || !Connected)
            return;

        var stamp = DateTime.UtcNow.AddSeconds(1).ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);

        var chatMessage =
            $"<message from='41c322a1-b328-495b-a004-5ccd3e45eae8@eu1.pvp.net/RC-Deceive' stamp='{stamp}' id='fake-{stamp}' type='chat'><body>{message}</body></message>";

        var bytes = Encoding.UTF8.GetBytes(chatMessage);
        await Incoming.WriteAsync(bytes).ConfigureAwait(false);
        Trace.WriteLine("<!--DECEIVE TO RC-->" + chatMessage);
    }

    public async Task UpdateStatusAsync(string newStatus)
    {
        if (string.IsNullOrEmpty(LastPresence) || !Connected)
            return;

        await PossiblyRewriteAndResendPresenceAsync(LastPresence, newStatus).ConfigureAwait(false);
    }

    private void OnConnectionErrored()
    {
        Connected = false;
        ConnectionErrored?.Invoke(this, EventArgs.Empty);
    }
}