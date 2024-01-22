/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCFloodProtectionConfig
 * @created     : Monday Jan 22, 2024 12:06:29 CST
 */

namespace GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class IRCFloodProtectionConfig : VConfig
{
	internal readonly VValue<bool> _enabled;

	public bool Enabled
	{
		get { return _enabled.Value; }
		set { _enabled.Value = value; }
	}

	internal readonly VValue<double> _rate;

	public double Rate
	{
		get { return _rate.Value; }
		set { _rate.Value = value; }
	}

	internal readonly VValue<int> _burst;

	public int Burst
	{
		get { return _burst.Value; }
		set { _burst.Value = value; }
	}

	internal readonly VValue<double> _joinDelay;

	public double JoinDelay
	{
		get { return _joinDelay.Value; }
		set { _joinDelay.Value = value; }
	}


	public IRCFloodProtectionConfig()
	{
		_enabled = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();

		_rate = AddValidatedValue<double>(this)
		    .Default(2.0)
		    .ChangeEventsEnabled();

		_burst = AddValidatedValue<int>(this)
		    .Default(5)
		    .ChangeEventsEnabled();

		_joinDelay = AddValidatedValue<double>(this)
		    .Default(1)
		    .ChangeEventsEnabled();
	}
}

