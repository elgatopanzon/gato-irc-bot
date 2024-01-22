/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCConfig
 * @created     : Monday Jan 22, 2024 11:55:20 CST
 */

namespace GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class IRCConfig : VConfig
{
	internal readonly VNative<IRCClientConfig> _client;

	public IRCClientConfig Client
	{
		get { return _client.Value; }
		set { _client.Value = value; }
	}

	internal readonly VValue<Dictionary<string, IRCNetworkConfig>> _networks;

	public Dictionary<string, IRCNetworkConfig> Networks
	{
		get { return _networks.Value; }
		set { _networks.Value = value; }
	}

	public IRCConfig()
	{
		_client = AddValidatedNative<IRCClientConfig>(this)
		    .Default(new IRCClientConfig())
		    .ChangeEventsEnabled();

		_networks = AddValidatedValue<Dictionary<string, IRCNetworkConfig>>(this)
		    .Default(new Dictionary<string, IRCNetworkConfig>())
		    .ChangeEventsEnabled();
	}
}

