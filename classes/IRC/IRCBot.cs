/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : IRCBot
 * @created     : Monday Jan 22, 2024 12:43:04 CST
 */

namespace GatoIRCBot.IRC;

using GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;
using GodotEGP.CLI;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;

using IrcDotNet;

public abstract partial class IRCBot : IDisposable
{
	private bool _isDisposed { get; set; }

	// holds bot's irc config
	private IRCConfig _ircConfig { get; set; }
	public IRCConfig IrcConfig {
		get {
			return _ircConfig;
		}
	}

	private IRCBotConfig _ircBotConfig { get; set; }
	public IRCBotConfig IrcBotConfig {
		get {
			return _ircBotConfig;
		}
	}

	// collection of clients (server-name => client-object)
    private Dictionary<string, IrcClient> _ircClients;

    // prefix used for commands
    private string _commandPrefix { get; set; } = "!";
    private static readonly Regex commandPartsSplitRegex = new Regex("(?<! /.*) ", RegexOptions.None);

    public string CommandPrefix { 
    	get {
			return _commandPrefix;
    	}
    }

	// CLI interface as command processor
	private IRCBotCommandLineInterface _cli;
	protected IRCBotCommandLineInterface CLI
	{
		get { return _cli; }
		set { _cli = value; }
	}

	public IRCBot(IRCConfig ircConfig, IRCBotConfig ircBotConfig)
	{
		_ircConfig = ircConfig;
		_ircBotConfig = ircBotConfig;

		LoggerManager.LogDebug("Creating bot instance with config", "", "config", _ircConfig);

		// holds a list of clients (one for each network)
		_ircClients = new();

        InitializeCommandLineInterface();
	}

	/*****************
	*  IDisposable  *
	*****************/
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // Disconnect each client gracefully.
                foreach (var client in _ircClients)
                {
                    if (client.Value != null)
                    {
                        client.Value.Quit(_ircConfig.Client.QuitTimeout, _ircConfig.Client.QuitMessage);
                        client.Value.Dispose();
                    }
                }
            }
        }

        _isDisposed = true;
    }

    public void Connect()
    {
    	// create clients for each network defined in the config
		foreach (var network in _ircConfig.Networks)
		{
			if (network.Value.Enabled)
			{
				LoggerManager.LogInfo("Connecting to network", "", "network", network.Key);

				ConnectToNetwork(network.Key);
			}
		}
    }

    public void Disconnect()
    {
    	// disconnect all clients from networks
		foreach (var network in _ircConfig.Networks)
		{
			if (network.Value.Enabled)
			{
				LoggerManager.LogInfo("Disconnecting from network", "", "network", network.Key);

				DisconnectFromNetwork(network.Key);
			}
		}
    }

    protected void ConnectToNetwork(string networkName)
    {
        var client = new StandardIrcClient();

        IRCNetworkConfig networkConfig = _ircConfig.Networks[networkName];

        // configure flood protection if enabled
        if (networkConfig.FloodProtection.Enabled)
        {
        	client.FloodPreventer = new IrcStandardFloodPreventer(networkConfig.FloodProtection.Burst, (long) networkConfig.FloodProtection.Rate);
        }

        // hook up events
        client.Connected += _On_Irc_Connected;
        client.Disconnected += _On_Irc_Disconnected;
        client.Registered += _On_Irc_Registered;

        // create user registration info object
        IRCClientConfig clientConfig = _ircConfig.Client.DeepCopy();
        clientConfig.MergeFrom(networkConfig.ClientConfigOverrides);

        IrcUserRegistrationInfo userInfo = new() {
			UserName = clientConfig.Nickname,
			NickName = clientConfig.Nickname,
			RealName = clientConfig.Realname,
			// UserModes = clientConfig.UserModes, TODO
        };

        // fetch the first server config for the network
        // TODO: implement some usage of multiple servers e.g. server cycling
        string serverName = "";
        foreach (var server in networkConfig.Servers)
        {
        	serverName = server.Key;
        }
        IRCNetworkServerConfig networkServer = networkConfig.Servers[serverName];

        LoggerManager.LogDebug("Creating IRC client instance", "", "connectionInfo", $"network: {networkName}, nick:{clientConfig.Nickname}, server:{networkServer.Hostname}:{networkServer.Port}");

		// connect the instance to the network and wait for a timeout
        using (var connectedEvent = new ManualResetEventSlim(false))
        {
            client.Connected += (sender2, e2) => connectedEvent.Set();
            client.Connect(networkServer.Hostname, networkServer.Port, networkServer.SSL, userInfo);

			// timeout the connection
            if (!connectedEvent.Wait(10000))
            {
                client.Dispose();
        		LoggerManager.LogError("Connection timed out", "", "connectionInfo", $"network: {networkName}, nick:{clientConfig.Nickname}, server:{networkServer.Hostname}:{networkServer.Port}");
                return;
            }
        }

        // join configured channels
		foreach (var channel in networkConfig.Channels)
		{
			if (channel.Value.JoinEnabled)
			{
				client.Channels.Join(channel.Key);
			}
		}

        // add connected client to clients list
        _ircClients.Add(networkName, client);

        LoggerManager.LogDebug("Connected!", "", "connectionInfo", $"network: {networkName}, nick:{clientConfig.Nickname}, server:{networkServer.Hostname}:{networkServer.Port}");
    }

    public void DisconnectFromNetwork(string networkName)
    {
        IRCNetworkConfig networkConfig = _ircConfig.Networks[networkName];

        IRCClientConfig clientConfig = _ircConfig.Client.DeepCopy();
        clientConfig.MergeFrom(networkConfig.ClientConfigOverrides);

        if (_ircClients.TryGetValue(networkName, out var client))
        {
        	
            client.Quit(clientConfig.QuitTimeout, clientConfig.QuitMessage);
            client.Dispose();

            _ircClients.Remove(networkName);

			LoggerManager.LogDebug("Disconnected from network", "", "network", networkName);
        }
    }

    /***********************
	*  IRC event methods  *
	***********************/
    // override methods to implement functionality
    protected virtual void OnClientConnect(IrcClient client, string networkName) { }
    protected virtual void OnClientDisconnect(IrcClient client, string networkName) { }
    protected virtual void OnClientRegistered(IrcClient client, string networkName) { }
    protected virtual void OnLocalUserJoinedChannel(IrcLocalUser localUser, IrcChannelEventArgs e, string networkName) { }
    protected virtual void OnLocalUserLeftChannel(IrcLocalUser localUser, IrcChannelEventArgs e, string networkName) { }
    protected virtual void OnLocalUserNoticeReceived(IrcLocalUser localUser, IrcMessageEventArgs e, string networkName) { }
    protected virtual void OnLocalUserMessageReceived(IrcLocalUser localUser, IrcMessageEventArgs e, string networkName, bool isChatCommand = false) { }
    protected virtual void OnChannelUserJoined(IrcChannel channel, IrcChannelUserEventArgs e, string networkName) { }
    protected virtual void OnChannelUserLeft(IrcChannel channel, IrcChannelUserEventArgs e, string networkName) { }
    protected virtual void OnChannelNoticeReceived(IrcChannel channel, IrcMessageEventArgs e, string networkName) { }
    protected virtual void OnChannelMessageReceived(IrcChannel channel, IrcMessageEventArgs e, string networkName, bool isBotHighlight, string textHighlightStripped, bool isChatCommand = false) { }

	/**************************
	*  Bot commands methods  *
	**************************/

	protected bool IsBotHighlight(IrcClient client, string message)
	{
		return (message.StartsWith($"{client.LocalUser.NickName}: "));
	}

	protected string StripBotHighlight(IrcClient client, string message)
	{
		if (IsBotHighlight(client, message))
		{
			return message.Replace($"{client.LocalUser.NickName}: ", "");
		}
		else
		{
			return message;
		}
	}
	
    private bool ReadChatCommand(IrcClient client, IrcMessageEventArgs eventArgs, bool isChannel = false)
    {
        // Check if given message represents chat command.
        var line = eventArgs.Text;

		// implement support for bot hightlight commands and limiting commands
		// to highlight only
        bool canReadCommand = true;

        if (_ircBotConfig.CommandsRequireHighlight && (!IsBotHighlight(client, line) && isChannel))
        {
			canReadCommand = false;
        }

        line = StripBotHighlight(client, line);

        if (canReadCommand && line.Length > 1 && line.StartsWith(_commandPrefix))
        {
            // Process command.
            var parts = commandPartsSplitRegex.Split(line.Substring(1)).Select(p => p.TrimStart('/')).ToArray();
            var command = parts.First();
            var parameters = parts.Skip(1).ToArray();
            ReadChatCommand(client, eventArgs.Source, eventArgs.Targets, command, parameters);
            return true;
        }
        return false;
    }

    private async void ReadChatCommand(IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets,
        string command, string[] parameters)
    {
        var defaultReplyTarget = GetDefaultReplyTarget(client, source, targets);

        // ChatCommandProcessor processor;
        if (CLI.IsCommand(command))
        {
            await System.Threading.Tasks.Task.Factory.StartNew(async () =>
            {
                try
                {
                    // processor(client, source, targets, command, parameters);
                    // setting CLI args
                    CLI.SetArgs(new string[] { command }.Concat(parameters).ToArray());

                    var commandFunc = CLI.GetCommandFunc(command);

                    LoggerManager.LogDebug("Executing chat command", "", "command", parameters);

        			var networkName = _ircClients.FirstOrDefault(x => x.Value == client).Key;

                    await CLI.ExecuteBotCommandFunction(commandFunc, command, CLI.GetArgumentValues(command), client, source, targets, networkName);
                }
                catch (InvalidCommandParametersException exInvalidCommandParameters)
                {
                    client.LocalUser.SendNotice(defaultReplyTarget,
                        exInvalidCommandParameters.GetMessage(command));
                }
                catch (UserNotAdminException)
                {
                    LoggerManager.LogDebug("Unauthorised command execution", "", "command", $"nick:{source.Name}, command:{command}, params:{String.Join(" ", parameters)}");

                    client.LocalUser.SendNotice(defaultReplyTarget, "No admin permissions!");
                }
                catch (Exception ex)
                {
                    if (source is IIrcMessageTarget)
                    {
                        client.LocalUser.SendNotice(defaultReplyTarget, $"Error processing '{command}' command: {ex.Message}");
                    }
                }
            }, System.Threading.Tasks.TaskCreationOptions.LongRunning);
        }
        else
        {
            if (source is IIrcMessageTarget)
            {
                client.LocalUser.SendNotice(defaultReplyTarget, $"Command '{command}' not recognized.");
            }
        }
    }

    public IList<IIrcMessageTarget> GetDefaultReplyTarget(IrcClient client, IIrcMessageSource source,
        IList<IIrcMessageTarget> targets)
    {
        if (targets.Contains(client.LocalUser) && source is IIrcMessageTarget)
            return new[] { (IIrcMessageTarget)source };
        else
            return targets;
    }

	protected abstract void InitializeCommandLineInterface();


    /**********************
	*  Callback methods  *
	**********************/
    
    private void _On_Irc_Connected(object sender, EventArgs e)
    {
        var client = (IrcClient) sender;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == client).Key;

        LoggerManager.LogDebug("Client connected", networkName, "network", networkName);

        OnClientConnect(client, networkName);
    }

    private void _On_Irc_Disconnected(object sender, EventArgs e)
    {
        var client = (IrcClient) sender;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == client).Key;

        LoggerManager.LogDebug("Client disconnected", networkName, "network", networkName);

        OnClientDisconnect(client, networkName);
    }

    private void _On_Irc_Registered(object sender, EventArgs e)
    {
        var client = (IrcClient) sender;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == client).Key;

        LoggerManager.LogDebug("Client registered", networkName, "network", networkName);

        client.LocalUser.NoticeReceived += _On_Irc_LocalUser_NoticeReceived;
        client.LocalUser.MessageReceived += _On_Irc_LocalUser_MessageReceived;
        client.LocalUser.JoinedChannel += _On_Irc_LocalUser_JoinedChannel;
        client.LocalUser.LeftChannel += _On_Irc_LocalUser_LeftChannel;

        OnClientRegistered(client, networkName);

        // identify with nickserv
        if (_ircConfig.Networks[networkName].NickservAuthentication)
        {
        	string nickservUser = _ircConfig.Networks[networkName].NickservUsername;
        	string nickservPass = _ircConfig.Networks[networkName].NickservPassword;

        	LoggerManager.LogDebug("Identifying with nickserv", "", "user", nickservUser);

        	client.LocalUser.SendMessage("NickServ", $"identify {nickservUser} {nickservPass}");
        }
    }

    public bool UserIgnored(IIrcMessageSource user)
    {
    	return _ircBotConfig.IgnoredNicknames.Contains(user.Name);
    }

    private void _On_Irc_LocalUser_NoticeReceived(object sender, IrcMessageEventArgs e)
    {
        var localUser = (IrcLocalUser) sender;
        if (UserIgnored(localUser))
        	return;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == localUser.Client).Key;

        LoggerManager.LogDebug("Notice received", networkName, $"sender:{e.Source.Name}", e.Text);

        OnLocalUserNoticeReceived(localUser, e, networkName);
    }

    private void _On_Irc_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
    {
        var localUser = (IrcLocalUser) sender;
        if (UserIgnored(localUser))
        	return;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == localUser.Client).Key;

        LoggerManager.LogDebug("Message received", networkName, $"sender:{e.Source.Name}", e.Text);

        OnLocalUserMessageReceived(localUser, e, networkName, isChatCommand:(e.Text.Replace($"{localUser.NickName}: ", "").StartsWith(_commandPrefix)));

        if (e.Source is IrcUser)
        {
            // Read message and process if it is chat command.
            if (ReadChatCommand(localUser.Client, e, isChannel:false))
            	return;
        }
    }

    private void _On_Irc_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
    {
        var localUser = (IrcLocalUser)sender;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == localUser.Client).Key;

        LoggerManager.LogDebug("Joined channel", networkName, $"nick:{localUser.NickName}", e.Channel.Name);

        e.Channel.UserJoined += _On_Irc_Channel_UserJoined;
        e.Channel.UserLeft += _On_Irc_Channel_UserLeft;
        e.Channel.MessageReceived += _On_Irc_Channel_MessageReceived;
        e.Channel.NoticeReceived += _On_Irc_Channel_NoticeReceived;

        OnLocalUserJoinedChannel(localUser, e, networkName);
    }

    private void _On_Irc_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
    {
        var localUser = (IrcLocalUser)sender;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == localUser.Client).Key;

        LoggerManager.LogDebug("Left channel", networkName, $"nick:{localUser.NickName}", e.Channel.Name);

        e.Channel.UserJoined -= _On_Irc_Channel_UserJoined;
        e.Channel.UserLeft -= _On_Irc_Channel_UserLeft;
        e.Channel.MessageReceived -= _On_Irc_Channel_MessageReceived;
        e.Channel.NoticeReceived -= _On_Irc_Channel_NoticeReceived;

        OnLocalUserLeftChannel(localUser, e, networkName);
    }

    private void _On_Irc_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
    {
        var channel = (IrcChannel)sender;
        if (UserIgnored(e.ChannelUser.User))
        	return;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == channel.Client).Key;

        LoggerManager.LogDebug("User joined channel", networkName, $"nick:{e.ChannelUser.User.NickName}", channel.Name);

        OnChannelUserJoined(channel, e, networkName);
    }

    private void _On_Irc_Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
    {
        var channel = (IrcChannel)sender;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == channel.Client).Key;

        LoggerManager.LogDebug("User left channel", networkName, $"nick:{e.ChannelUser.User.NickName}", channel.Name);

        OnChannelUserLeft(channel, e, networkName);
    }

    private void _On_Irc_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
    {
        var channel = (IrcChannel)sender;
        if (UserIgnored(e.Source))
        	return;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == channel.Client).Key;

        LoggerManager.LogDebug("Channel notice received", networkName, $"channel:{channel.Name}", e.Text);

        OnChannelNoticeReceived(channel, e, networkName);
    }

    private void _On_Irc_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
    {
        var channel = (IrcChannel)sender;
        if (UserIgnored(e.Source))
        	return;

        var networkName = _ircClients.FirstOrDefault(x => x.Value == channel.Client).Key;

        LoggerManager.LogDebug("Channel message received", networkName, $"channel:{channel.Name}", e.Text);

        OnChannelMessageReceived(channel, e, networkName, IsBotHighlight(channel.Client, e.Text), StripBotHighlight(channel.Client, e.Text), isChatCommand:(e.Text.Replace($"{channel.Client.LocalUser.NickName}: ", "").StartsWith(_commandPrefix)));

        if (e.Source is IrcUser)
        {
            // Read message and process if it is chat command.
            if (ReadChatCommand(channel.Client, e, isChannel:true))
            	return;
        }
    }


    protected delegate void ChatCommandProcessor(IrcClient client, IIrcMessageSource source,
        IList<IIrcMessageTarget> targets, string command, IList<string> parameters);
}

/****************
*  Exceptions  *
****************/

public class UserNotAdminException : Exception
{
	public UserNotAdminException() { }
	public UserNotAdminException(string message) : base(message) { }
	public UserNotAdminException(string message, Exception inner) : base(message, inner) { }
	protected UserNotAdminException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
}

public class InvalidCommandParametersException : Exception
{
    public InvalidCommandParametersException(int minParameters, int? maxParameters = null)
        : base()
    {
        Debug.Assert(minParameters >= 0,
            "minParameters must be at least zero.");
        Debug.Assert(maxParameters == null || maxParameters >= minParameters,
            "maxParameters must be at least minParameters.");

        this.MinParameters = minParameters;
        this.MaxParameters = maxParameters ?? minParameters;
    }

    public int MinParameters
    {
        get;
        private set;
    }

    public int MaxParameters
    {
        get;
        private set;
    }

    public override string Message
    {
        get
        {
            throw new NotSupportedException();
        }
    }

    public string GetMessage(string command)
    {
        if (this.MinParameters == 0 && this.MaxParameters == 0)
            return string.Format("Command {0} takes no arguments.", command);
        else if (this.MinParameters == this.MaxParameters)
            return string.Format("Command {0} takes {1} arguments.", command,
                this.MinParameters);
        else
            return string.Format("Command {0} takes {1} to {2} arguments.", command,
                this.MinParameters, this.MaxParameters);
    }
}
