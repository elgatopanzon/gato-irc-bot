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
using GodotEGP.AI.GatoGPT;

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

	// token cache for chat messages
	private ChatTokenCache _chatTokenCache { get; set; }

	public Gato(IRCConfig config, IRCBotConfig botConfig) : base(config, botConfig)
	{
		_config = ServiceRegistry.Get<ConfigManager>().Get<GatoConfig>();

		_openAiService = ServiceRegistry.Get<OpenAIService>();

		_chatTokenCache = new();
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

    public void EraseMessageHistoryForClientSource(string networkName, string sourceName)
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

		WriteChatMessageToHistory(sourceHistory, message);
    }

    public void WriteChatMessageToHistory(ChatMessageHistory sourceHistory, ChatMessage message)
    {
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
    		var historyLinesCount = historyLines.Count();

    		historyLines = historyLines.Reverse().Take(_config.ModelProfile.MaxHistoryLines).Reverse().ToArray();

			// check if history exceeds max lines * 2, then cycle history to a
			// new file and re-write current one
    		if (historyLinesCount > (_config.ModelProfile.MaxHistoryLines * 2))
    		{
    			LoggerManager.LogDebug("History exceeds max lines * 2", "", "history", $"history:{chatHistoryPath}, lines:{historyLinesCount}, maxLines:{_config.ModelProfile.MaxHistoryLines}");

				// erase/backup current history file
    			EraseMessageHistoryForClientSource(sourceHistory.NetworkName, sourceHistory.SourceName);

				File.WriteAllLines(chatHistoryPath, historyLines);
    		}

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
    public async void QueueOpenAIChatCompletionsRequest(IrcClient client, IList<IIrcMessageTarget> replyTarget, ChatMessageHistory sourceHistory, ChatMessage latestMessage)
    {
    	LoggerManager.LogDebug("Queuing OpenAI chat request", sourceHistory.NetworkName, "sourceName", sourceHistory.SourceName);
    	LoggerManager.LogDebug("", "", "chatMessage", latestMessage);

    	// get chat history formatted as ChatCompletionsRequest
    	ChatCompletionRequest r = await GetChatMessageHistoryAsChatCompletionsRequest(sourceHistory);

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

    public async Task<ChatCompletionRequest> GetChatMessageHistoryAsChatCompletionsRequest(ChatMessageHistory sourceHistory)
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

		// apply extended params if enabled and configured
    	if (_config.ModelProfile.UseGatoGPTExtended)
    	{
    		r.Extended = _config.ModelProfile.Extended.DeepCopy();

			// set the prompt cache ID based on network and source
			if (r.Extended == null)
			{
				r.Extended = new();
			}

			if (r.Extended.Inference == null)
			{
				r.Extended.Inference = new();
			}
			if (r.Extended.Model == null)
			{
				r.Extended.Model = new();
			}

    		if (r.Extended != null && r.Extended.Inference != null && r.Extended.Model != null)
    		{
    			r.Extended.Model.PromptCache = true;
    			r.Extended.Inference.PromptCacheId = $"GatoGPT-{IrcConfig.Client.Nickname}-{sourceHistory.NetworkName}-{sourceHistory.SourceName}";
    		}
    	}

    	if (_config.ModelProfile.Inference.Seed != null)
    	{
    		r.Seed = (int) _config.ModelProfile.Inference.Seed;
    	}

    	// tokenize chat messages and add them to the request object up to the
    	// max history size
    	r = await BuildChatCompletionMessages(r, sourceHistory);

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

    public async Task<ChatCompletionRequest> BuildChatCompletionMessages(ChatCompletionRequest request, ChatMessageHistory sourceHistory, int tokenizationStrategy = 1)
    {
		// inject some system prompt information
    	var systemPrompts = _config.DefaultSystemPrompts.DeepCopy();

		// pick up profile system prompts instead if any are set
    	if (_config.ModelProfile.SystemPrompts.Count > 0)
    	{
    		systemPrompts = _config.ModelProfile.SystemPrompts.DeepCopy();
    	}

    	systemPrompts.Add($"Your name is {IrcConfig.Client.Nickname} and you are {(sourceHistory.IsChannel ? "talking in an IRC channel named "+sourceHistory.SourceName : "talking to "+sourceHistory+" on IRC")} on {sourceHistory.NetworkName}");

		// add additional system prompts
		systemPrompts = systemPrompts.Concat(_config.AdditionalSystemPrompts).ToList();

		List<ChatCompletionRequestMessage> chatMessages = new();

    	// add chat history messages to list of chat messages
    	foreach (var message in sourceHistory.ChatMessages)
    	{
    		if (message.Content == null)
    		{
    			continue;
    		}

			var msg = new ChatCompletionRequestMessage() {
    			Role = ((message.Nickname == IrcConfig.Client.Nickname) ? "assistant" : "user"),
    			Name = message.Nickname,
				Content = message.Content,
    			};

    		chatMessages.Add(msg);
    	}

		int requestTokenCount = 0;
		int maxTokenCount = _config.ModelProfile.HistoryTokenSize - (int) _config.ModelProfile.Inference.MaxTokens;

		// add system prompts to list of chat messages (at the end, since we'll
		// reverse them and we want them first
    	foreach (var systemPrompt in systemPrompts)
    	{
			chatMessages.Add(new() {
				Content = systemPrompt,
				Role = "system",
				});
    	}

    	LoggerManager.LogDebug("Chat history message count", "", "chatMessagesCount", chatMessages.Count);

		// reverse messages
		chatMessages.Reverse();

		// cache each message and build up the full request
		// problem: due to underlying models prompt formats we end up with more
		// tokens than what we'd normally have with a single request and there's
		// no way to know up front
		if (tokenizationStrategy == 0)
		{
			// loop through chat messages and build up the real request's chat
			// messages by counting tokens up to token limit
			for (int i = 0; i < chatMessages.Count; i++)
			{
				var message = chatMessages[i];

    			LoggerManager.LogDebug("Tokenizing message", "", i.ToString(), message);
    			LoggerManager.LogDebug("Messages remaining", "", "remaining", chatMessages.Count - (i + 1));

				var tokens = await GetTokenizedChatMessage(message, _config.ModelProfile.Inference.Model);
				int tokenizedCount = tokens.Count();

				if (requestTokenCount + tokenizedCount > maxTokenCount)
				{
					break;
				}

    			LoggerManager.LogDebug("Current request token size", "", "requestTokenCount", $"{requestTokenCount} / {maxTokenCount}");

				request.Messages.Add(message);
				requestTokenCount += tokenizedCount;
			}
		}

		// subtractive method, start with the full request's messages and
		// calculate the percent that it's over, removing messages until it fits
		// in the allowed token limits
		else if (tokenizationStrategy == 1)
		{
			// set request's chat messages
			request.Messages = chatMessages;

			// start with the initial request
			if (requestTokenCount == 0)
			{
				var fullTokens = await GatoGPTTokenizeChat(request);
				int fullTokenCount = fullTokens.Count();

				LoggerManager.LogDebug("Full message history token count", "", "fullTokenCount", fullTokenCount);


				requestTokenCount = fullTokenCount;
			}

			// calculate the percent that we're over the limit, and remove half
			// of many messages from the history
			// e.g. if there's 500 messages, and we're 20% over, then we remove
			// 10% of 500 = 50 messages to remove
			while (requestTokenCount > maxTokenCount)
			{
				var tokens = await GatoGPTTokenizeChat(request);
				requestTokenCount = tokens.Count();

				double tokenPercentLimit = (double) ((double) requestTokenCount / (double) maxTokenCount);

				LoggerManager.LogDebug("Request limit percent", "", "tokenPercentLimit", tokenPercentLimit);

				// if the percent is > 1.0 then it's over the limits
				if (tokenPercentLimit > 1.0)
				{
					double tokenPercentOver = tokenPercentLimit - 1.0;
					LoggerManager.LogDebug("Request over limit percent", "", "tokenPercentOver", tokenPercentOver);

					int messagesToRemove = Math.Max(5, Convert.ToInt32((request.Messages.Count) * (tokenPercentOver * 0.5)));
					LoggerManager.LogDebug("Removing messages", "", "messagesToRemove", messagesToRemove);

					request.Messages = request.Messages.SkipLast(messagesToRemove).ToList();
				}
			}
		}
		// fake tokenization when not allowed to use extended
		else if (tokenizationStrategy == -1)
		{
			// TODO
		}

    	request.Messages.Reverse();

    	LoggerManager.LogDebug("Final request token size", "", "requestTokenCount", $"{requestTokenCount} / {maxTokenCount}");

    	return request;
    }

    public async Task<List<TokenizedString>> GetTokenizedChatMessage(ChatCompletionRequestMessage message, string modelId)
    {
    	string messageString = $"{message.Role}{message.Name}{message.Content}";
    	List<TokenizedString> tokens = _chatTokenCache.GetCache(modelId, messageString);

		// if null then it's a cache miss
		if (tokens == null)
		{
			// construct a ChatCompletionRequest using this message and the
			// provided modelId
			ChatCompletionRequest request = new();
			request.Messages = new();
			request.Messages.Add(message);
			request.Model = modelId;

			// set generation template and cfg negatice prompt to empty to not
			// increase token size
			request.Extended = new() {
				Inference = new() {
					ChatMessageGenerationTemplate = "",
					CfgNegativePrompt = "",
					PrePrompt = "",
				}
			};

			// retrieve tokenized message and save it in the cache
			tokens = await GatoGPTTokenizeChat(request);
			_chatTokenCache.StoreCache(modelId, messageString, tokens);

			LoggerManager.LogDebug("Storing tokenized message cache", "", "message", message);
		}
		else
		{
			LoggerManager.LogDebug("Retrieved tokenized cache for message", "", "message", message);
		}

		return tokens;
    }

	// fake Tokenize method using the 100,000 words = 75,000 tokens estimate
	public int GetFakeTokenCount(string content)
	{
		int c = content.Split(new char[] { ' ', '!', '<', '>', '/', '?', '[', ']' }).Count();
		return Convert.ToInt32(((double) c) * 2);
	}

	public void StopGeneration(ChatMessageHistory sourceHistory)
	{
		foreach (var request in _ongoingOpenAIRequests)
		{
			if (request.Value.SourceHistory.Equals(sourceHistory))
			{
				// unsubscribe object from all events
				request.Value.RequestObject.OpenAI.UnsubscribeAll();

				// remove from requests
				_ongoingOpenAIRequests.Remove(request.Key);
			}
		}
	}

	public async Task<List<TokenizedString>> GatoGPTTokenizeString(string content)
	{
		// create GatoGPT instance
		var gatoGpt = new GatoGPT(ServiceRegistry.Get<ConfigManager>().Get<GlobalConfig>().OpenAIConfig);

		// get the tokenized result
		var res = await gatoGpt.Tokenize(new TokenizeRequest() {
			Model = _config.ModelProfile.Inference.Model,
			Content = content,
			});

		LoggerManager.LogDebug("Tokenize string result", "", "res", res);

		if (res == null)
		{
			return null;
		}

		return res.Tokens;

	}
	public async Task<List<TokenizedString>> GatoGPTTokenizeChat(ChatCompletionRequest request)
	{
		// create GatoGPT instance
		var gatoGpt = new GatoGPT(ServiceRegistry.Get<ConfigManager>().Get<GlobalConfig>().OpenAIConfig);

		// get the tokenized result
		var res = await gatoGpt.TokenizeChat(request);

		LoggerManager.LogDebug("Tokenize chat result", "", "res", res);

		return res.Tokens;
	}

    /*****************************
	 *  Message process methods  *
	 *****************************/
    public async void ProcessIncomingMessage(IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string networkName, string line, bool isChannel = false, bool isHighlight = false, bool isChatCommand = false)
    {
    	// obtain the source history object for this client-source
		var sourceHistory = InitMessageHistoryForClientSource(client, source, targets, networkName, isChannel);
		var defaultReplyTarget = GetDefaultReplyTarget(client, source, targets);

		// check for improved highlight (bots name appears in text
		if (!isHighlight)
		{
			string pattern = $@"\b{client.LocalUser.NickName}\b";
			isHighlight = Regex.IsMatch(line, pattern);

			LoggerManager.LogDebug("Extended highlight matching", client.LocalUser.NickName, pattern, isHighlight);
		}

		// parse /me into displayed text
		if (line.StartsWith("\u0001ACTION "))
		{
			line = line.Replace("\u0001", "");
			line = line.Replace("ACTION", source.Name);
			line = $"*{line}*";

			LoggerManager.LogDebug("Action detected", "", "line", line);
		}

		// strip non-printable characters from line
		line = Regex.Replace(line, @"\p{C}+", string.Empty);

		// strip the generation suffix to stop it being included in other bot's
		// history
		line = line.Replace(_config.GenerationFinishedSuffix, string.Empty);

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

		// return if admin only mode is enabled and user isn't admin
		if (!IsAdmin(source) && _config.AdminOnlyMode)
		{
			LoggerManager.LogDebug("Admin only mode is enabled");

			return;
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

			QueueOpenAIChatCompletionsRequest(client, defaultReplyTarget, sourceHistory, chatMessage);
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

		// only strip an unfinished sentence if it's the final line
    	// if (e.IsLast)
		// 	e.Text = StripUnfinishedSentence(e.Text);

		// only output if we're streaming lines
    	if (_config.StreamingLines)
    	{
    		string replyLine = e.Text;

    		if (e.IsLast)
    		{
    			replyLine += _config.GenerationFinishedSuffix;
    		}

        	SendIrcMessage(requestHolder, replyLine);
    	}
    }

    public string StripUnfinishedSentence(string message)
    {
        if (_config.StripUnfinishedSentences)
        {
        	var r = Regex.Match(message, @"(^.*[\.\?!]|^\S[^.\?!]*)");
        	
        	LoggerManager.LogDebug("Stripping unfinished sentence from line", "", "line", message);

        	message = r.ToString();
        }

        return message;
    }

    public void SendIrcMessage(ChatCompletionRequestHolder requestHolder, string message)
    {
        if (CanTalkOnNetworkSource(requestHolder.SourceHistory.NetworkName, requestHolder.SourceHistory.SourceName))
        {
			// split the string on spaces
        	foreach (var msg in message.SplitOnLength(350))
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

    	if (requestHolder == null)
    	{
    		LoggerManager.LogError("Failed to get request holder", "", "result", e.Result);
    		return;
    	}

    	// save the full response into the chat history
    	ChatMessage chatMessage = new() {
			Content = e.Result.Choices[0].Message.GetContent(),
			Nickname = IrcConfig.Client.Nickname,
    	};

		SaveChatMessage(requestHolder.SourceHistory, chatMessage);

		// if we have streaming lines disabled, we need to send the full
		// response
    	if (!_config.StreamingLines)
    	{

    		LoggerManager.LogDebug("Found request holder", "", "requestSource", $"network:{requestHolder.SourceHistory.NetworkName}, source:{requestHolder.SourceHistory.SourceName}, trigger:{requestHolder.RequestOriginal.Messages.Last().GetContent()}");

    		string replyLine = e.Result.Choices[0].Message.GetContent();
			// replyLine = StripUnfinishedSentence(replyLine);

        	requestHolder.IrcClient.LocalUser.SendMessage(requestHolder.ReplyTarget, replyLine);
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
		ProcessIncomingMessage(channel.Client, e.Source, e.Targets, networkName, e.Text, isChannel:true, isHighlight:isBotHighlight, isChatCommand:isChatCommand);
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
