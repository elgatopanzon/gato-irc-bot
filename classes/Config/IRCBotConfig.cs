/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCBotConfig
 * @created     : Monday Jan 22, 2024 17:00:11 CST
 */

namespace GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;
using GodotEGP.CLI;

public partial class IRCBotConfig : VConfig
{
	internal readonly VValue<string> _chatCommandPrefix;

	public string ChatCommandPrefix
	{
		get { return _chatCommandPrefix.Value; }
		set { _chatCommandPrefix.Value = value; }
	}

	internal readonly VValue<List<string>> _adminNicknames;

	public List<string> AdminNicknames
	{
		get { return _adminNicknames.Value; }
		set { _adminNicknames.Value = value; }
	}

	internal readonly VValue<bool> _commandsRequireHighlight;

	public bool CommandsRequireHighlight
	{
		get { return _commandsRequireHighlight.Value; }
		set { _commandsRequireHighlight.Value = value; }
	}

	public IRCBotConfig()
	{
		_chatCommandPrefix = AddValidatedValue<string>(this)
		    .Default("!")
		    .ChangeEventsEnabled();

		_adminNicknames = AddValidatedValue<List<string>>(this)
		    .Default(new List<string>())
		    .ChangeEventsEnabled();

		_commandsRequireHighlight = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();
	}
}

