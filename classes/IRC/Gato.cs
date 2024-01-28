/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : Gato
 * @created     : Monday Jan 22, 2024 16:22:17 CST
 */

namespace GatoIRCBot.IRC;

using GatoIRCBot.Config;
using GatoIRCBot.Service;
using GatoIRCBot.Event;

using Godot;
using GodotEGP;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;
using GodotEGP.AI.OpenAI;

using IrcDotNet;
using Newtonsoft.Json;

using System.Text.RegularExpressions;

public partial class Gato : IRCBotBase
{
	// main config
	private GatoConfig _config;

	public GatoConfig Config { 
		get {
			return _config;
		}
	}

	// hold ChatHistory instances per-client, per-source
	private Dictionary<string, Dictionary<string, ChatMessageHistory>> _messageHistory { get; set; } = new();

	// base path to save chat history json files
	private string _chatMessagesBasePath { get; set; } = "user://ChatMessages";

	// openAI service 
	private OpenAIService _openAiService;

	// openAI ongoing requests map
	private Dictionary<OpenAiRequest, ChatCompletionRequestHolder> _ongoingOpenAIRequests { get; set; } = new();

	public Gato(IRCConfig config, IRCBotConfig botConfig) : base(config, botConfig)
	{
		_config = ServiceRegistry.Get<ConfigManager>().Get<GatoConfig>();

		_openAiService = ServiceRegistry.Get<OpenAIService>();
	}

    protected override void InitializeCommandLineInterface()
    {
    	CLI = new GatoBotCommandLineInterface(this);
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
				SourceName = sourceName,
				NetworkName = networkName,
				IsChannel = isChannel,
				Source = source,
				Targets = targets
			};

			// load any chat history into the existing instance
    		string chatLoadPath = Path.Combine(ProjectSettings.GlobalizePath(_chatMessagesBasePath), networkName, sourceName, "History.log");

			LoadChatHistory(sourceHistory, chatLoadPath);

			clientHistory.Add(sourceName, sourceHistory);

			LoggerManager.LogDebug("Initialised history for source", networkName, "source", sourceHistory.SourceName);
			LoggerManager.LogDebug("", networkName, "existingMessages", sourceHistory.ChatMessages.Count);
		}


		return sourceHistory;
    }

    public void ReloadMessageHistoryForClientSource(IrcClient client, string networkName, string sourceName)
    {
    	var clientHistory = InitMessageHistoryForClient(client, networkName);

		if (clientHistory.TryGetValue(sourceName, out var h))
		{
			clientHistory.Remove(sourceName);
			h = null;
		}
    }

    public void EraseMessageHistoryForClientSource(IrcClient client, string networkName, string sourceName)
    {
    	string chatHistoryPath = Path.Combine(ProjectSettings.GlobalizePath(_chatMessagesBasePath), networkName, sourceName, "History.log");

		// backup and erase history
		File.Move(chatHistoryPath, chatHistoryPath+$".bk-{((DateTimeOffset) DateTime.Now).ToUnixTimeSeconds()}");
    }

    public ChatMessageHistory GetHistoryFromClient(IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string networkName)
    {
    	ChatMessageHistory h = null;

		var networkHistory = InitMessageHistoryForClient(client, networkName);

		foreach (var history in networkHistory)
		{
			LoggerManager.LogDebug(history.Value.SourceName);
			if (history.Value.SourceName == source.Name || history.Value.SourceName == String.Join(" ", targets))
			{
				return history.Value;
			}
		}

    	return h;
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
    	File.AppendAllText(Path.Combine(chatSavePath, "History.log"), parsedMessage.Replace("\n", " ")+"\n");
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


    /********************
	*  OpenAI methods  *
	********************/
    public void QueueOpenAIChatCompletionsRequest(IrcClient client, IList<IIrcMessageTarget> replyTarget, ChatMessageHistory sourceHistory, ChatMessage latestMessage)
    {
    	LoggerManager.LogDebug("Queuing OpenAI chat request", sourceHistory.NetworkName, "sourceName", sourceHistory.SourceName);
    	LoggerManager.LogDebug("", "", "chatMessage", latestMessage);

    	// get chat history formatted as ChatCompletionsRequest
    	ChatCompletionRequest r = GetChatMessageHistoryAsChatCompletionsRequest(sourceHistory);

    	LoggerManager.LogDebug("ChatCompletionsRequest", "", "r", r);

		// queue the request and subscribe to events
    	var requestObj = _openAiService.QueueChatCompletion(r);

    	requestObj.SubscribeOwner<OpenAIChatCompletionLine>(_On_OpenAI_ChatCompletionLine, isHighPriority:true);
    	requestObj.SubscribeOwner<OpenAIChatCompletionError>(_On_OpenAI_ChatCompletionError, isHighPriority:true);
    	requestObj.SubscribeOwner<OpenAIChatCompletionResult>(_On_OpenAI_ChatCompletionResult, isHighPriority:true);

    	var requestHolder = new ChatCompletionRequestHolder() {
			RequestObject = requestObj,
			RequestOriginal = r,
			IrcClient = client,
			SourceHistory = sourceHistory,
			ReplyTarget = replyTarget,
    	};

    	_ongoingOpenAIRequests.Add(requestObj, requestHolder);
    }

    public ChatCompletionRequest GetChatMessageHistoryAsChatCompletionsRequest(ChatMessageHistory sourceHistory)
    {
    	ChatCompletionRequest r = new();
    	r.Messages = new();
    	r.Stream = true;
    	r.Model = _config.ModelProfile.Inference.Model;
    	r.MaxTokens = _config.ModelProfile.Inference.MaxTokens;
    	r.PresencePenalty = _config.ModelProfile.Inference.PresencePenalty;
    	r.FrequencyPenalty = _config.ModelProfile.Inference.FrequencyPenalty;
    	r.Temperature = _config.ModelProfile.Inference.Temperature;
    	r.TopP = _config.ModelProfile.Inference.TopP;

    	if (_config.ModelProfile.Inference.Seed != null)
    	{
    		r.Seed = (int) _config.ModelProfile.Inference.Seed;
    	}

    	// fill up a list in reverse counting the token size
    	int currentTokenSize = 0;

    	List<ChatMessage> messages = new();

		// inject some system prompt information
    	var systemPrompts = _config.DefaultSystemPrompts.DeepCopy();

		// pick up profile system prompts instead if any are set
    	if (_config.ModelProfile.SystemPrompts.Count > 0)
    	{
    		systemPrompts = _config.ModelProfile.SystemPrompts.DeepCopy();
    	}

    	systemPrompts.Add($"The date is {DateTime.Today}");
    	systemPrompts.Add($"Your name is {IrcConfig.Client.Nickname}");
    	// systemPrompts.Add($"You are talking on the {sourceHistory.NetworkName} IRC network to {sourceHistory.SourceName}");


    	// add system prompts counts
    	foreach (var systemPrompt in systemPrompts)
    	{
    		currentTokenSize += GetFakeTokenCount(systemPrompt);
    	}

    	// create message objects for each history message in reverse until we
    	// hit the token limit
    	foreach (var message in sourceHistory.ChatMessages.AsEnumerable().Reverse().ToList())
    	{
    		if (message.Content == null)
    		{
    			continue;
    		}
    		int messageTokenSize = GetFakeTokenCount(message.Content);

			if ((currentTokenSize + messageTokenSize) + _config.ModelProfile.Inference.MaxTokens <= _config.ModelProfile.HistoryTokenSize)
			{   
				var msg = new ChatCompletionRequestMessage() {
    				Role = ((message.Nickname == IrcConfig.Client.Nickname) ? "assistant" : "user"),
    				Name = message.Nickname,
					Content = message.Content,
    				};

    			r.Messages.Add(msg);

    			currentTokenSize += messageTokenSize;
			}
			else
			{
				LoggerManager.LogDebug("History token limit hit", "", "limit", _config.ModelProfile.HistoryTokenSize);

				break;
			}
    	}

    	LoggerManager.LogDebug("History current token limit", "", "tokens", currentTokenSize);

    	foreach (var systemPrompt in systemPrompts.AsEnumerable().Reverse())
    	{
			r.Messages.Add(new() {
				Content = systemPrompt,
				Role = "system",
				});
    	}

		// re-reverse messages
    	r.Messages.Reverse();


		// convert content to an object if there's images
		var lastMessage = sourceHistory.ChatMessages.Last();
		if (lastMessage.Images.Count > 0)
		{
			var contentObj = new List<Dictionary<string, object>>();

			contentObj.Add(new() {
				{ "type", "text"},
				{ "text", lastMessage.Content},
				});

			foreach (var image in lastMessage.Images)
			{
				contentObj.Add(new() {
					{ "type", "image_url"},
					{ "image_url", new Dictionary<string, object>() {
						{ "url", image }
						}
					},
					});
			}

			// set last message to include images
			r.Messages.Last().Content = contentObj;
		}


    	return r;
    }

	// fake Tokenize method using the 100,000 words = 75,000 tokens estimate
	public int GetFakeTokenCount(string content)
	{
		int c = Math.Max(1, (content.Split(" ").Length));
		return Convert.ToInt32(c * 0.75);
	}

    /*****************************
	 *  Message process methods  *
	 *****************************/
    public async void ProcessIncomingMessage(IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string networkName, string line, bool isChannel = false, bool isHighlight = false, bool isChatCommand = false)
    {
    	// obtain the source history object for this client-source
		var sourceHistory = InitMessageHistoryForClientSource(client, source, targets, networkName, isChannel);
		var defaultReplyTarget = GetDefaultReplyTarget(client, source, targets);

		// return if admin only mode is enabled and user isn't admin
		if (!IsAdmin(source) && _config.AdminOnlyMode)
		{
        	client.LocalUser.SendNotice(defaultReplyTarget, "Admin only mode is enabled");

			return;
		}

		// store message as message object in history instance
		ChatMessage chatMessage = new() {
			Nickname = source.Name,
					 Content = line,
					 IsBotHighlight = isHighlight,
		};

		LoggerManager.LogDebug("Chat message object", networkName, "chatMessage", chatMessage);

		// trigger a message history save for this incoming client-source
		// note: not for commands
		if (!isChatCommand)
		{
			SaveChatMessage(sourceHistory, chatMessage);
		}

		// bot highlights make the bot trigger a message to the LLM, and a
		// response
		if ((isHighlight || !IsNetworkSourceHighlightRequired(networkName, sourceHistory.SourceName)) && !isChatCommand)
		{
			// parse image urls and add them as image data
			var imagesMatch = Regex.Match(chatMessage.Content, @"(https?:)?//?[^\'""<>]+?\.(jpg|jpeg|gif|png)");

			List<string> images = new();

			while (imagesMatch.Success)
			{
				images.Add(imagesMatch.Value);

				imagesMatch = imagesMatch.NextMatch();
			}

			if (images.Count > 0)
			{
				LoggerManager.LogDebug("Found image URLs in message", "", "images", images);

				chatMessage.Images = images;
			}

			QueueOpenAIChatCompletionsRequest(client, defaultReplyTarget, sourceHistory,  chatMessage);
		}
    }


	public bool IsAdmin(IIrcMessageSource source)
	{
		return (IrcBotConfig.AdminNicknames.Contains(source.Name));
	}

    public bool IsNetworkSourceHighlightRequired(string networkName, string sourceName)
    {
    	bool highlightRequired = IrcBotConfig.CommandsRequireHighlight;

    	if (_config.ReplyWithoutHighlight.TryGetValue(networkName, out var networkConfig))
    	{
    		if (networkConfig.Contains(sourceName))
    		{
    			highlightRequired = false;
    		}
    	}

    	return highlightRequired;
    }

    public ChatCompletionRequestHolder GetRequestHolder(OpenAiRequest requestObj)
    {
    	if (_ongoingOpenAIRequests.TryGetValue(requestObj, out var obj))
    	{
    		return obj;
    	}

    	return null;
    }

    public void ProcessChatCompletionLineEvent(OpenAIChatCompletionLine e)
    {
    	// find the request holder object
    	var requestHolder = GetRequestHolder((e.Owner as OpenAiRequest));

    	if (requestHolder == null)
    	{
    		LoggerManager.LogError("Failed to get request holder", "");
    		return;
    	}

    	LoggerManager.LogDebug("Found request holder", "", "requestSource", $"network:{requestHolder.SourceHistory.NetworkName}, source:{requestHolder.SourceHistory.SourceName}, trigger:{requestHolder.RequestOriginal.Messages.Last().GetContent()}");

		// if streaming lines is enabled, write the response line + the
		// configured TypingString
    	if (_config.StreamingLines)
    	{
    		string replyLine = e.Text;

    		if (!e.IsLast)
    		{
    			replyLine += _config.IsTypingSuffix;
    		}

        	SendIrcMessage(requestHolder, replyLine);
    	}
    }

    public void SendIrcMessage(ChatCompletionRequestHolder requestHolder, string message)
    {
        if (CanTalkOnNetworkSource(requestHolder.SourceHistory.NetworkName, requestHolder.SourceHistory.SourceName))
        {
        	if (_config.StripUnfinishedSentences)
        	{
        		var r = Regex.Match(message, @"(^.*[\.\?!]|^\S[^.\?!]*)");
        		
        		LoggerManager.LogDebug("Stripping unfinished sentence from line", "", "line", message);

        		message = r.ToString();
        	}

			// split the string on spaces
        	foreach (var msg in message.Trim().SplitOnLength(350))
        	{
        		requestHolder.IrcClient.LocalUser.SendMessage(requestHolder.ReplyTarget, msg);
        	}
        }

    	LoggerManager.LogDebug("Talk not enabled for source", "", "requestSource", $"network:{requestHolder.SourceHistory.NetworkName}, source:{requestHolder.SourceHistory.SourceName}, trigger:{requestHolder.RequestOriginal.Messages.Last().GetContent()}, response:{message}");
    }

    public bool CanTalkOnNetworkSource(string networkName, string sourceName)
    {
        bool talkEnabled = true;

		if (IrcConfig.Networks.TryGetValue(networkName, out var networkConfig))
		{
			if (networkConfig.Channels.TryGetValue(sourceName, out var channelConfig))
			{
				talkEnabled = channelConfig.TalkEnabled;
			}
		}

		return talkEnabled;
    }

    /**********************
	*  OpenAI callbacks  *
	**********************/
    public void _On_OpenAI_ChatCompletionLine(OpenAIChatCompletionLine e)
    {
    	if (e.IsLast)
    	{
    		LoggerManager.LogDebug("Chat completion line last", "", "line", e.Text);
    	}
    	else
    	{
    		LoggerManager.LogDebug("Chat completion line", "", "line", e.Text);
    	}

		ProcessChatCompletionLineEvent(e);
    }

    public void _On_OpenAI_ChatCompletionError(OpenAIChatCompletionError e)
    {
    	LoggerManager.LogDebug("Chat completion error", "", "error", e.Error);

    	var requestHolder = GetRequestHolder((e.Owner as OpenAiRequest));

        requestHolder.IrcClient.LocalUser.SendNotice(requestHolder.ReplyTarget, e.Error.Error.Message);
    }

    public void _On_OpenAI_ChatCompletionResult(OpenAIChatCompletionResult e)
    {
    	LoggerManager.LogDebug("Chat completion result", "", "result", e.Result);

    	var requestHolder = GetRequestHolder((e.Owner as OpenAiRequest));

    	// save the full response into the chat history
    	ChatMessage chatMessage = new() {
			Content = e.Result.Choices[0].Message.GetContent().Trim(),
			Nickname = IrcConfig.Client.Nickname,
    	};

		SaveChatMessage(requestHolder.SourceHistory, chatMessage);

		// if we have streaming lines disabled, we need to send the full
		// response
    	if (!_config.StreamingLines)
    	{

    		if (requestHolder == null)
    		{
    			LoggerManager.LogError("Failed to get request holder", "", "result", e.Result);
    			return;
    		}

    		LoggerManager.LogDebug("Found request holder", "", "requestSource", $"network:{requestHolder.SourceHistory.NetworkName}, source:{requestHolder.SourceHistory.SourceName}, trigger:{requestHolder.RequestOriginal.Messages.Last().GetContent()}");

    		string replyLine = e.Result.Choices[0].Message.GetContent();

        	requestHolder.IrcClient.LocalUser.SendMessage(requestHolder.ReplyTarget, replyLine.Trim());
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
    protected override void OnLocalUserMessageReceived(IrcLocalUser localUser, IrcMessageEventArgs e, string networkName, bool isChatCommand = false)
    {
		ProcessIncomingMessage(localUser.Client, e.Source, e.Targets, networkName, e.Text, isChannel:false, isHighlight:true, isChatCommand:isChatCommand);
    }
    protected override void OnChannelUserJoined(IrcChannel channel, IrcChannelUserEventArgs e, string networkName) { }
    protected override void OnChannelUserLeft(IrcChannel channel, IrcChannelUserEventArgs e, string networkName) { }
    protected override void OnChannelNoticeReceived(IrcChannel channel, IrcMessageEventArgs e, string networkName) { }
    protected override void OnChannelMessageReceived(IrcChannel channel, IrcMessageEventArgs e, string networkName, bool isBotHighlight, string textHighlightStripped, bool isChatCommand = false)
    {
		ProcessIncomingMessage(channel.Client, e.Source, e.Targets, networkName, textHighlightStripped, isChannel:true, isHighlight:isBotHighlight, isChatCommand:isChatCommand);
    }
}

public partial class ChatCompletionRequestHolder
{
	public OpenAiRequest RequestObject { get; set; }
	public ChatCompletionRequest RequestOriginal { get; set; }
	public IrcClient IrcClient { get; set; }
	public IList<IIrcMessageTarget> ReplyTarget { get; set; }
	public ChatMessageHistory SourceHistory { get; set; }
}
