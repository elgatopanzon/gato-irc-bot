/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : MessageHistory
 * @created     : Monday Jan 22, 2024 19:57:10 CST
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

public partial class ChatMessageHistory : VObject
{
	internal readonly VValue<string> _sourceName;

	public string SourceName
	{
		get { return _sourceName.Value; }
		set { _sourceName.Value = value; }
	}

	internal readonly VValue<string> _networkName;

	public string NetworkName
	{
		get { return _networkName.Value; }
		set { _networkName.Value = value; }
	}

	internal readonly VValue<bool> _isChannel;

	public bool IsChannel
	{
		get { return _isChannel.Value; }
		set { _isChannel.Value = value; }
	}

	internal readonly VValue<IIrcMessageSource> _source;

	internal IIrcMessageSource Source
	{
		get { return _source.Value; }
		set { _source.Value = value; }
	}

	internal readonly VValue<IList<IIrcMessageTarget>> _targets;

	internal IList<IIrcMessageTarget> Targets
	{
		get { return _targets.Value; }
		set { _targets.Value = value; }
	}

	internal readonly VValue<List<ChatMessage>> _chatMessages;

	public List<ChatMessage> ChatMessages
	{
		get { return _chatMessages.Value; }
		set { _chatMessages.Value = value; }
	}


	public ChatMessageHistory()
	{
		_sourceName = AddValidatedValue<string>(this)
		    .Default("")
		    .ChangeEventsEnabled();

		_networkName = AddValidatedValue<string>(this)
		    .Default("")
		    .ChangeEventsEnabled();

		_isChannel = AddValidatedValue<bool>(this)
		    .Default(false)
		    .ChangeEventsEnabled();

		_source = AddValidatedValue<IIrcMessageSource>(this)
		    .ChangeEventsEnabled();

		_targets = AddValidatedValue<IList<IIrcMessageTarget>>(this)
		    .ChangeEventsEnabled();

		_chatMessages = AddValidatedValue<List<ChatMessage>>(this)
		    .Default(new List<ChatMessage>())
		    .ChangeEventsEnabled();
	}
}

