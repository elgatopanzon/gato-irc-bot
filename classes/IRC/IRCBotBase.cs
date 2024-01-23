/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCBotBase
 * @created     : Monday Jan 22, 2024 16:42:26 CST
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
using GodotEGP.CLI;

using IrcDotNet;

public partial class IRCBotBase : IRCBot
{
	public IRCBotBase(IRCConfig config, IRCBotConfig botConfig) : base(config, botConfig)
	{
		
	}

    protected override void InitializeCommandLineInterface()
    {
    	CLI = new IRCBotCommandLineInterface(this);
    }
}

