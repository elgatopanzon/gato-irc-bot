/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCChannelConfig
 * @created     : Monday Jan 22, 2024 12:05:42 CST
 */

namespace GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class IRCChannelConfig : VConfig
{
	internal readonly VValue<bool> _joinEnabled;

	public bool JoinEnabled
	{
		get { return _joinEnabled.Value; }
		set { _joinEnabled.Value = value; }
	}

	internal readonly VValue<bool> _talkEnabled;

	public bool TalkEnabled
	{
		get { return _talkEnabled.Value; }
		set { _talkEnabled.Value = value; }
	}

	public IRCChannelConfig()
	{
		_joinEnabled = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();

		_talkEnabled = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();
	}
}

