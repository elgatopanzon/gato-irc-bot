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
	internal readonly VValue<bool> _enabled;

	public bool Enabled
	{
		get { return _enabled.Value; }
		set { _enabled.Value = value; }
	}

	public IRCChannelConfig()
	{
		_enabled = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();
	}
}

