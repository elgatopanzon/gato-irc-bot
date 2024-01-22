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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IrcDotNet;

public partial class IRCBot : IDisposable
{
	private bool _isDisposed { get; set; }

	// holds bot's irc config
	private IRCConfig _ircConfig { get; set; }

	// collection of clients (server-name => client-object)
    private Dictionary<string, IrcClient> _ircClients;

	public IRCBot(IRCConfig ircConfig)
	{
		_ircConfig = ircConfig;

		LoggerManager.LogDebug("Creating bot instance with config", "", "config", _ircConfig);

		// holds a list of clients (one for each network)
		_ircClients = new();
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
			if (channel.Value.Enabled)
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
    protected virtual void OnClientConnect(IrcClient client) { }
    protected virtual void OnClientDisconnect(IrcClient client) { }
    protected virtual void OnClientRegistered(IrcClient client) { }
    protected virtual void OnLocalUserJoinedChannel(IrcLocalUser localUser, IrcChannelEventArgs e) { }
    protected virtual void OnLocalUserLeftChannel(IrcLocalUser localUser, IrcChannelEventArgs e) { }
    protected virtual void OnLocalUserNoticeReceived(IrcLocalUser localUser, IrcMessageEventArgs e) { }
    protected virtual void OnLocalUserMessageReceived(IrcLocalUser localUser, IrcMessageEventArgs e) { }
    protected virtual void OnChannelUserJoined(IrcChannel channel, IrcChannelUserEventArgs e) { }
    protected virtual void OnChannelUserLeft(IrcChannel channel, IrcChannelUserEventArgs e) { }
    protected virtual void OnChannelNoticeReceived(IrcChannel channel, IrcMessageEventArgs e) { }
    protected virtual void OnChannelMessageReceived(IrcChannel channel, IrcMessageEventArgs e) { }


    /**********************
	*  Callback methods  *
	**********************/
    
    private void _On_Irc_Connected(object sender, EventArgs e)
    {
        var client = (IrcClient) sender;

        LoggerManager.LogDebug("Client connected", "", "network", _ircClients.FirstOrDefault(x => x.Value == client).Key);

        OnClientConnect(client);
    }

    private void _On_Irc_Disconnected(object sender, EventArgs e)
    {
        var client = (IrcClient) sender;

        LoggerManager.LogDebug("Client disconnected", "", "network", _ircClients.FirstOrDefault(x => x.Value == client).Key);

        OnClientDisconnect(client);
    }

    private void _On_Irc_Registered(object sender, EventArgs e)
    {
        var client = (IrcClient) sender;

        LoggerManager.LogDebug("Client registered", "", "network", _ircClients.FirstOrDefault(x => x.Value == client).Key);

        client.LocalUser.NoticeReceived += _On_Irc_LocalUser_NoticeReceived;
        client.LocalUser.MessageReceived += _On_Irc_LocalUser_MessageReceived;
        client.LocalUser.JoinedChannel += _On_Irc_LocalUser_JoinedChannel;
        client.LocalUser.LeftChannel += _On_Irc_LocalUser_LeftChannel;

        OnClientRegistered(client);
    }

    private void _On_Irc_LocalUser_NoticeReceived(object sender, IrcMessageEventArgs e)
    {
        var localUser = (IrcLocalUser) sender;

        LoggerManager.LogDebug("Notice received", "", $"sender:{e.Source.Name}", e.Text);

        OnLocalUserNoticeReceived(localUser, e);
    }

    private void _On_Irc_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
    {
        var localUser = (IrcLocalUser) sender;

        LoggerManager.LogDebug("Message received", "", $"sender:{e.Source.Name}", e.Text);

        if (e.Source is IrcUser)
        {
            // Read message and process if it is chat command.
            // TODO: implement method to process a command
            // if (ReadChatCommand(localUser.Client, e))
            //     return;
        }

        OnLocalUserMessageReceived(localUser, e);
    }

    private void _On_Irc_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
    {
        var localUser = (IrcLocalUser)sender;

        LoggerManager.LogDebug("Joined channel", "", $"nick:{localUser.NickName}", e.Channel.Name);

        e.Channel.UserJoined += _On_Irc_Channel_UserJoined;
        e.Channel.UserLeft += _On_Irc_Channel_UserLeft;
        e.Channel.MessageReceived += _On_Irc_Channel_MessageReceived;
        e.Channel.NoticeReceived += _On_Irc_Channel_NoticeReceived;

        OnLocalUserJoinedChannel(localUser, e);
    }

    private void _On_Irc_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
    {
        var localUser = (IrcLocalUser)sender;

        LoggerManager.LogDebug("Left channel", "", $"nick:{localUser.NickName}", e.Channel.Name);

        e.Channel.UserJoined -= _On_Irc_Channel_UserJoined;
        e.Channel.UserLeft -= _On_Irc_Channel_UserLeft;
        e.Channel.MessageReceived -= _On_Irc_Channel_MessageReceived;
        e.Channel.NoticeReceived -= _On_Irc_Channel_NoticeReceived;

        OnLocalUserLeftChannel(localUser, e);
    }

    private void _On_Irc_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
    {
        var channel = (IrcChannel)sender;

        LoggerManager.LogDebug("User joined channel", "", $"nick:{e.ChannelUser.User.NickName}", channel.Name);

        OnChannelUserJoined(channel, e);
    }

    private void _On_Irc_Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
    {
        var channel = (IrcChannel)sender;

        LoggerManager.LogDebug("User left channel", "", $"nick:{e.ChannelUser.User.NickName}", channel.Name);

        OnChannelUserLeft(channel, e);
    }

    private void _On_Irc_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
    {
        var channel = (IrcChannel)sender;

        LoggerManager.LogDebug("Channel notice received", "", $"channel:{channel.Name}", e.Text);

        OnChannelNoticeReceived(channel, e);
    }

    private void _On_Irc_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
    {
        var channel = (IrcChannel)sender;

        LoggerManager.LogDebug("Channel message received", "", $"channel:{channel.Name}", e.Text);

        if (e.Source is IrcUser)
        {
            // Read message and process if it is chat command.
            // TODO: implement method to process a command
            // if (ReadChatCommand(channel.Client, e))
            //     return;
        }

        OnChannelMessageReceived(channel, e);
    }
}
