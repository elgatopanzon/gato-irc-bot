/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : ChatMessage
 * @created     : Monday Jan 22, 2024 20:03:58 CST
 */

namespace GatoIRCBot.IRC;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

using IrcDotNet;

public partial class ChatMessage : VObject
{
	internal readonly VValue<DateTime> _timestamp;

	public DateTime Timestamp
	{
		get { return _timestamp.Value; }
		set { _timestamp.Value = value; }
	}

	internal readonly VValue<string> _nickname;

	public string Nickname
	{
		get { return _nickname.Value; }
		set { _nickname.Value = value; }
	}

	internal readonly VValue<string> _content;

	public string Content
	{
		get { return _content.Value; }
		set { _content.Value = value; }
	}

	internal readonly VValue<bool> _isBotHighlight;

	public bool IsBotHighlight
	{
		get { return _isBotHighlight.Value; }
		set { _isBotHighlight.Value = value; }
	}

	public ChatMessage()
	{
		_timestamp = AddValidatedValue<DateTime>(this)
	    	.Default(DateTime.Now)
	    	.ChangeEventsEnabled();

		_nickname = AddValidatedValue<string>(this)
	    	.ChangeEventsEnabled();

		_content = AddValidatedValue<string>(this)
		    .ChangeEventsEnabled();

		_isBotHighlight = AddValidatedValue<bool>(this)
		    .Default(false)
		    .ChangeEventsEnabled();
	}
}

