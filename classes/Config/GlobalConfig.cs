/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : GlobalConfig
 * @created     : Monday Jan 22, 2024 22:17:25 CST
 */

namespace GodotEGP.Config;

using GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class GlobalConfig : VConfig
{
	internal VNative<OpenAIConfig> _openAiConfig = new();

	public OpenAIConfig OpenAIConfig
	{
		get { return _openAiConfig.Value; }
		set { _openAiConfig.Value = value; }
	}

	partial void InitConfigParams()
	{
		_openAiConfig = AddValidatedNative<OpenAIConfig>(this)
		    .Default(new OpenAIConfig())
		    .ChangeEventsEnabled();
	}
}

