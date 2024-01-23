/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : SaveDataManagementConfig
 * @created     : Monday Jan 22, 2024 22:18:26 CST
 */

namespace GodotEGP.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class SaveDataManagerConfig : VObject
{
	partial void InitConfigParams()
	{
		// disable creation of System data
		AutocreateSystemData = false;
	}
}
