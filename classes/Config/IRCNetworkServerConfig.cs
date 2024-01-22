/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCNetworkServerConfig
 * @created     : Monday Jan 22, 2024 12:11:07 CST
 */

namespace GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class IRCNetworkServerConfig : VConfig
{
	internal readonly VValue<bool> _enabled;

	public bool Enabled
	{
		get { return _enabled.Value; }
		set { _enabled.Value = value; }
	}

	internal readonly VValue<string> _hostname;

	public string Hostname
	{
		get { return _hostname.Value; }
		set { _hostname.Value = value; }
	}

	internal readonly VValue<int> _port;

	public int Port
	{
		get { return _port.Value; }
		set { _port.Value = value; }
	}

	internal readonly VValue<bool> _SSL;

	public bool SSL
	{
		get { return _SSL.Value; }
		set { _SSL.Value = value; }
	}

	public IRCNetworkServerConfig()
	{
		_enabled = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();
		
		_hostname = AddValidatedValue<string>(this)
		    .ChangeEventsEnabled();

		_port = AddValidatedValue<int>(this)
		    .Default(6667)
		    .ChangeEventsEnabled();

		_SSL = AddValidatedValue<bool>(this)
		    .Default(false)
		    .ChangeEventsEnabled();
	}
}

