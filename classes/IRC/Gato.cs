/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : Gato
 * @created     : Monday Jan 22, 2024 16:22:17 CST
 */

namespace GatoIRCBot.IRC;

using GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

using IrcDotNet;

public partial class Gato : IRCBotBase
{
	public Gato(IRCConfig config, IRCBotConfig botConfig) : base(config, botConfig)
	{
		
	}

    protected override void InitializeCommandLineInterface()
    {
    	CLI = new IRCBotCommandLineInterface(this);
    }

	/*************************
	*  IRC event callbacks  *
	*************************/
    protected override void OnClientConnect(IrcClient client) { }
    protected override void OnClientDisconnect(IrcClient client) { }
    protected override void OnClientRegistered(IrcClient client) { }
    protected override void OnLocalUserJoinedChannel(IrcLocalUser localUser, IrcChannelEventArgs e) { }
    protected override void OnLocalUserLeftChannel(IrcLocalUser localUser, IrcChannelEventArgs e) { }
    protected override void OnLocalUserNoticeReceived(IrcLocalUser localUser, IrcMessageEventArgs e) { }
    protected override void OnLocalUserMessageReceived(IrcLocalUser localUser, IrcMessageEventArgs e) { }
    protected override void OnChannelUserJoined(IrcChannel channel, IrcChannelUserEventArgs e) { }
    protected override void OnChannelUserLeft(IrcChannel channel, IrcChannelUserEventArgs e) { }
    protected override void OnChannelNoticeReceived(IrcChannel channel, IrcMessageEventArgs e) { }
    protected override void OnChannelMessageReceived(IrcChannel channel, IrcMessageEventArgs e) { }
}
