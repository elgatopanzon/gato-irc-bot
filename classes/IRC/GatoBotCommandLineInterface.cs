/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : GatoBotCommandLineInterface
 * @created     : Friday Jan 26, 2024 23:06:11 CST
 */

namespace GatoIRCBot.IRC;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

using GodotEGP.CLI;

using IrcDotNet;

public partial class GatoBotCommandLineInterface : IRCBotCommandLineInterface
{
	public GatoBotCommandLineInterface(IRCBot ircBot) : base(ircBot)
	{
		
	}
}

