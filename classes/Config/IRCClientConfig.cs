/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCClientConfig
 * @created     : Monday Jan 22, 2024 11:56:12 CST
 */

namespace GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class IRCClientConfig : VConfig
{
	internal readonly VValue<string> _nickname;

	public string Nickname
	{
		get { return _nickname.Value; }
		set { _nickname.Value = value; }
	}

	internal readonly VValue<string> _nicknameSuffix;

	public string NicknameSuffix
	{
		get { return _nicknameSuffix.Value; }
		set { _nicknameSuffix.Value = value; }
	}

	internal readonly VValue<string> _realname;

	public string Realname
	{
		get { return _realname.Value; }
		set { _realname.Value = value; }
	}

	internal readonly VValue<string> _quitMessage;

	public string QuitMessage
	{
		get { return _quitMessage.Value; }
		set { _quitMessage.Value = value; }
	}

	internal readonly VValue<int> _quitTimeout;

	public int QuitTimeout
	{
		get { return _quitTimeout.Value; }
		set { _quitTimeout.Value = value; }
	}

	public IRCClientConfig()
	{
		_nickname = AddValidatedValue<string>(this)
		    .Default("Gato")
		    .ChangeEventsEnabled();

		_nicknameSuffix = AddValidatedValue<string>(this)
		    .Default("_")
		    .ChangeEventsEnabled();

		_realname = AddValidatedValue<string>(this)
	    	.Default(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)
	    	.ChangeEventsEnabled();

		_quitMessage = AddValidatedValue<string>(this)
		    .Default("Meow!")
		    .ChangeEventsEnabled();

		_quitTimeout = AddValidatedValue<int>(this)
		    .Default(1000)
		    .ChangeEventsEnabled();
	}
}

