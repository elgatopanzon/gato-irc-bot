/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCBotCommandLineInterface
 * @created     : Monday Jan 22, 2024 18:05:40 CST
 */

namespace GatoIRCBot.IRC;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;
using GodotEGP.CLI;

using IrcDotNet;

public partial class IRCBotCommandLineInterface : CommandLineInterface
{
	protected IRCBot _ircBot;

	protected string _ircCommand;
	protected List<string> _ircCommandParameters;
	protected IrcClient _ircClient;
	protected IIrcMessageSource _ircMessageSource;
	protected IList<IIrcMessageTarget> _ircMessageTargets;
	protected IList<IIrcMessageTarget> _ircReplyTarget;
	protected string _ircNetworkName;

	public IRCBotCommandLineInterface(IRCBot ircBot)
	{
		_ircBot = ircBot;

		// override the help command
		_commands["help"] = (BotCommandHelp, "List help", true);
		_commands["quit"] = (BotCommandQuit, "Shut down the bot", false);
	}

	public async Task<int> ExecuteBotCommandFunction(Func<Task<int>> commandFunc, string command, List<string> parameters, IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string networkName)
	{
		// set irc client values to use in the command
		_ircCommand = command;
		_ircCommandParameters = parameters;
		_ircClient = client;
		_ircMessageSource = source;
		_ircMessageTargets = targets;
		_ircNetworkName = networkName;
        _ircReplyTarget = _ircBot.GetDefaultReplyTarget(_ircClient, _ircMessageSource, _ircMessageTargets);

		// run the command
		return await commandFunc();
	}

	/**************************
	*  Bot helper functions  *
	**************************/
	
	public bool IsAdmin()
	{
		return (_ircBot.IrcBotConfig.AdminNicknames.Contains(_ircMessageSource.Name));
	}

	/**************
	*  Commands  *
	**************/

	public virtual async Task<int> BotCommandHelp()
	{
        _ircClient.LocalUser.SendMessage(_ircReplyTarget, "Available commands:");

        foreach (var cmd in _commands)
        {
        	string commandHelp = $"{cmd.Key}";

        	if (_commandArgs.TryGetValue(cmd.Key, out var cmdArgs) && cmdArgs.Count > 0)
        	{
        		foreach (var arg in cmdArgs)
        		{
        			commandHelp += $" [{arg.Arg} e.g. {arg.Example}]";
        		}
        	}

			_ircClient.LocalUser.SendMessage(_ircReplyTarget, commandHelp);
        }

		return 0;
	}

	public virtual async Task<int> BotCommandQuit()
	{
		if (!IsAdmin()) { throw new UserNotAdminException(); }

		_ircBot.Disconnect();

		return 0;
	}
}
