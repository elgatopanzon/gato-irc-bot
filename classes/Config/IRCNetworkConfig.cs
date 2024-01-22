/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCNetworkConfig
 * @created     : Monday Jan 22, 2024 12:02:36 CST
 */

namespace GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class IRCNetworkConfig : VConfig
{
	internal readonly VNative<IRCClientConfig> _clientConfigOverrides;

	public IRCClientConfig ClientConfigOverrides
	{
		get { return _clientConfigOverrides.Value; }
		set { _clientConfigOverrides.Value = value; }
	}

	internal readonly VValue<bool> _enabled;

	public bool Enabled
	{
		get { return _enabled.Value; }
		set { _enabled.Value = value; }
	}

	internal readonly VValue<Dictionary<string, IRCNetworkServerConfig>> _servers;

	public Dictionary<string, IRCNetworkServerConfig> Servers
	{
		get { return _servers.Value; }
		set { _servers.Value = value; }
	}

	internal readonly VValue<Dictionary<string, IRCChannelConfig>> _channels;

	public Dictionary<string, IRCChannelConfig> Channels
	{
		get { return _channels.Value; }
		set { _channels.Value = value; }
	}


	internal readonly VNative<IRCFloodProtectionConfig> _floodProtection;

	public IRCFloodProtectionConfig FloodProtection
	{
		get { return _floodProtection.Value; }
		set { _floodProtection.Value = value; }
	}

	public IRCNetworkConfig()
	{
		_clientConfigOverrides = AddValidatedNative<IRCClientConfig>(this)
		    .Default(new IRCClientConfig())
		    .ChangeEventsEnabled();

		_enabled = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();

		_servers = AddValidatedValue<Dictionary<string, IRCNetworkServerConfig>>(this)
		    .Default(new Dictionary<string, IRCNetworkServerConfig>())
		    .ChangeEventsEnabled();

		_channels = AddValidatedValue<Dictionary<string, IRCChannelConfig>>(this)
		    .Default(new Dictionary<string, IRCChannelConfig>())
		    .ChangeEventsEnabled();

		_floodProtection = AddValidatedNative<IRCFloodProtectionConfig>(this)
		    .Default(new IRCFloodProtectionConfig())
		    .ChangeEventsEnabled();
	}
}

