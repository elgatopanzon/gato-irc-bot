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

	internal readonly VValue<string> _sessionName;

	public string SessionName
	{
		get { return _sessionName.Value; }
		set { _sessionName.Value = value; }
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

		_sessionName = AddValidatedValue<string>(this)
		    .Default("History")
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

	public List<ChatMessage> GetLastMessages(int count = 0)
	{
		if (count == 0)
		{
			return ChatMessages;
		}

		return ChatMessages.Skip(Math.Max(0, ChatMessages.Count() - count)).ToList();
	}

	public void EraseLastMessages(int count = 0)
	{
		ChatMessages = ChatMessages.SkipLast(count).ToList();
	}

	public void EditMessage(int idFromLast, string contentNew)
	{
		int idActual = ChatMessages.Count - idFromLast;

		ChatMessages[idActual].Content = contentNew;
	}
}

