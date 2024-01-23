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

	public IRCBotCommandLineInterface(IRCBot ircBot)
	{
		_ircBot = ircBot;

		// override the help command
		_commands["help"] = (BotCommandHelp, "List help", false);
	}

	public virtual async Task<int> BotCommandHelp()
	{
        _ircClient.LocalUser.SendMessage(_ircReplyTarget, "Available commands:");
        _ircClient.LocalUser.SendMessage(_ircReplyTarget, string.Join(", ",
            _commands.Where(x => x.Value.includeInHelp).Select(kvPair => kvPair.Key)));

		return 0;
	}

	public void ExecuteBotCommandFunction(Func<Task<int>> commandFunc, string command, List<string> parameters, IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets)
	{
		// set irc client values to use in the command
		_ircCommand = command;
		_ircCommandParameters = parameters;
		_ircClient = client;
		_ircMessageSource = source;
		_ircMessageTargets = targets;
        _ircReplyTarget = _ircBot.GetDefaultReplyTarget(_ircClient, _ircMessageSource, _ircMessageTargets);

		// run the command
		commandFunc();
	}
}
