/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : Gato
 * @created     : Monday Jan 22, 2024 16:22:17 CST
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

using IrcDotNet;
using Newtonsoft.Json;

using System.Text.RegularExpressions;

public partial class Gato : IRCBotBase
{
	// hold ChatHistory instances per-client, per-source
	private Dictionary<string, Dictionary<string, ChatMessageHistory>> _messageHistory { get; set; } = new();

	// base path to save chat history json files
	private string _chatMessagesBasePath { get; set; } = "user://ChatMessages";

	public Gato(IRCConfig config, IRCBotConfig botConfig) : base(config, botConfig)
	{

	}

    protected override void InitializeCommandLineInterface()
    {
    	CLI = new IRCBotCommandLineInterface(this);
    }

    /********************************
	 *  History management methods  *
	 ********************************/

    public Dictionary<string, ChatMessageHistory> InitMessageHistoryForClient(IrcClient client, string networkName)
    {
		if (!_messageHistory.TryGetValue(networkName, out var clientHistory))
		{
			clientHistory = new();
			_messageHistory.Add(networkName, clientHistory);

			LoggerManager.LogDebug("Initialised history for client", networkName, "network", networkName);
		}

		return clientHistory;
    }

    public ChatMessageHistory InitMessageHistoryForClientSource(IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string networkName, bool isChannel)
    {
    	var clientHistory = InitMessageHistoryForClient(client, networkName);

		// if its a PM then the source is the ID, otherwise it's the channel
		string sourceName = source.Name;
		if (isChannel)
		{
			sourceName = String.Join(" ", targets);
		}

		if (!clientHistory.TryGetValue(sourceName, out var sourceHistory))
		{
			sourceHistory = new() {
				SourceId = sourceName,
						 IsChannel = isChannel,
						 Source = source,
						 Targets = targets
			};

			// load any chat history into the existing instance
    		string chatLoadPath = Path.Combine(ProjectSettings.GlobalizePath(_chatMessagesBasePath), networkName, sourceName, "History.log");

			LoadChatHistory(sourceHistory, chatLoadPath);

			clientHistory.Add(sourceName, sourceHistory);

			LoggerManager.LogDebug("Initialised history for source", networkName, "source", sourceHistory.SourceId);
			LoggerManager.LogDebug("", networkName, "existingMessages", sourceHistory.ChatMessages.Count);
		}


		return sourceHistory;
    }

    public void SaveMessageHistoryJson(ChatMessageHistory sourceHistory)
    {
    	// find the network name from the chat history list
    	string chatSavePath = GetChatMessagesSavePath(sourceHistory);
    	Directory.CreateDirectory(chatSavePath);

    	File.WriteAllText(Path.Combine(chatSavePath, "History.json"), JsonConvert.SerializeObject(sourceHistory), System.Text.Encoding.UTF8);
    }

    public (string NetworkName, string SourceName) GetNetworkSourceName(ChatMessageHistory sourceHistory)
    {
    	string networkName = "";
    	string sourceName = "";

    	foreach (var history in _messageHistory)
    	{
    		foreach (var source in history.Value)
    		{
    			if (source.Value.Equals(sourceHistory))
    			{
    				networkName = history.Key;
    				sourceName = source.Key;
    			}
    		}
    	}

    	return (networkName, sourceName);
    }

    public string GetChatMessagesSavePath(ChatMessageHistory sourceHistory)
    {
    	var messagesSavePath = GetNetworkSourceName(sourceHistory);

    	string networkName = messagesSavePath.NetworkName;
    	string sourceName = messagesSavePath.SourceName;

    	return Path.Combine(ProjectSettings.GlobalizePath(_chatMessagesBasePath), networkName, sourceName);
    }

    public void SaveChatMessage(ChatMessageHistory sourceHistory, ChatMessage message)
    {
    	// add message to current source object
		sourceHistory.ChatMessages.Add(message);

    	// parse chat message into text log
    	string parsedMessage = $"[{message.Timestamp.ToString()}] <{message.Nickname}> {message.Content}";

    	LoggerManager.LogDebug("Saving chat message line", "", "messageParsed", parsedMessage);

    	// create the chat messages save directory
    	string chatSavePath = GetChatMessagesSavePath(sourceHistory);
    	Directory.CreateDirectory(chatSavePath);

    	// append text content as line to history file
    	File.AppendAllText(Path.Combine(chatSavePath, "History.log"), parsedMessage+"\n");
    }

    public void LoadChatHistory(ChatMessageHistory sourceHistory, string chatHistoryPath)
    {
    	if (File.Exists(chatHistoryPath))
    	{
    		var historyLines = File.ReadAllLines(chatHistoryPath);

    		foreach (var line in historyLines)
    		{
    			// LoggerManager.LogDebug("Parsing chat history line", "", "messageLine", line);

    			ChatMessage chatMessage = new();

    			var r = new Regex(@"\[([\d]+/[\d]+/[\d]+ [\d]+:[\d]+:[\d]+)\] \<([\w]+)\> (.+)", RegexOptions.IgnoreCase);
    			Match m = r.Match(line);

				while (m.Success)
      			{
         			for (int i = 1; i <= 3; i++)
         			{
            			Group g = m.Groups[i];
            			CaptureCollection cc = g.Captures;
            			for (int j = 0; j < cc.Count; j++)
            			{
               				Capture c = cc[j];

               				if (i == 1)
               				{
								// timestamp
								chatMessage.Timestamp = DateTime.Parse(c.ToString());
               				}
               				else if (i == 2)
               				{
								// nickname
								chatMessage.Nickname = c.ToString();
               				}
               				else if (i == 3)
               				{
								// content
								chatMessage.Content = c.ToString();
               				}
            			}
         			}
         			m = m.NextMatch();
      			}

      			// LoggerManager.LogDebug("Parsed chat message", "", "chatMessage", chatMessage);

      			sourceHistory.ChatMessages.Add(chatMessage);
    		}
    	}
    }

    /*****************************
	 *  Message process methods  *
	 *****************************/
    public void ProcessIncomingMessage(IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string networkName, string line, bool isChannel = false, bool isHighlight = false)
    {
    	// obtain the source history object for this client-source
		var sourceHistory = InitMessageHistoryForClientSource(client, source, targets, networkName, isChannel);

		// store message as message object in history instance
		ChatMessage chatMessage = new() {
			Nickname = source.Name,
					 Content = line,
					 IsBotHighlight = isHighlight,
		};

		LoggerManager.LogDebug("Chat message object", networkName, "chatMessage", chatMessage);

		// trigger a message history save for this incoming client-source
		SaveChatMessage(sourceHistory, chatMessage);

		// bot highlights make the bot trigger a message to the LLM, and a
		// response
		if (isHighlight)
		{
			LoggerManager.LogTrace("TODO: process message here");
		}
    }

	/*************************
	 *  IRC event callbacks  *
	 *************************/
    protected override void OnClientConnect(IrcClient client, string networkName) { }
    protected override void OnClientDisconnect(IrcClient client, string networkName) { }
    protected override void OnClientRegistered(IrcClient client, string networkName)
    {
		// init the message history instance for this client
    }
    protected override void OnLocalUserJoinedChannel(IrcLocalUser localUser, IrcChannelEventArgs e, string networkName) { }
    protected override void OnLocalUserLeftChannel(IrcLocalUser localUser, IrcChannelEventArgs e, string networkName) { }
    protected override void OnLocalUserNoticeReceived(IrcLocalUser localUser, IrcMessageEventArgs e, string networkName) { }
    protected override void OnLocalUserMessageReceived(IrcLocalUser localUser, IrcMessageEventArgs e, string networkName)
    {
		ProcessIncomingMessage(localUser.Client, localUser, e.Targets, networkName, e.Text, isChannel:false, isHighlight:false);
    }
    protected override void OnChannelUserJoined(IrcChannel channel, IrcChannelUserEventArgs e, string networkName) { }
    protected override void OnChannelUserLeft(IrcChannel channel, IrcChannelUserEventArgs e, string networkName) { }
    protected override void OnChannelNoticeReceived(IrcChannel channel, IrcMessageEventArgs e, string networkName) { }
    protected override void OnChannelMessageReceived(IrcChannel channel, IrcMessageEventArgs e, string networkName, bool isBotHighlight, string textHighlightStripped)
    {
		ProcessIncomingMessage(channel.Client, e.Source, e.Targets, networkName, textHighlightStripped, isChannel:true, isHighlight:isBotHighlight);
    }
}
