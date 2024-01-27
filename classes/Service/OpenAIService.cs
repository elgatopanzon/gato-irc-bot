/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : OpenAIService
 * @created     : Monday Jan 22, 2024 23:01:58 CST
 */

namespace GatoIRCBot.Service;

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

using Newtonsoft.Json;
using System.Text.RegularExpressions;

public partial class OpenAIService : Service
{
	private OpenAIConfig _openAIConfig { get; set; }

	// queue of api requests
	private Queue<OpenAiRequest> _requestQueue { get; set; } = new();

	// list of ongoing api requests
	private List<OpenAiRequest> _ongoingRequests { get; set; } = new();

	public OpenAIService()
	{
		_openAIConfig = ServiceRegistry.Get<ConfigManager>().Get<GlobalConfig>().OpenAIConfig;
	}

	/*******************
	*  Godot methods  *
	*******************/

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_SetServiceReady(true);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		ProcessRequestQueue();
	}

	/*********************
	*  Service methods  *
	*********************/

	// Called when service is registered in manager
	public override void _OnServiceRegistered()
	{
	}

	// Called when service is deregistered from manager
	public override void _OnServiceDeregistered()
	{
		// LoggerManager.LogDebug($"Service deregistered!", "", "service", this.GetType().Name);
	}

	// Called when service is considered ready
	public override void _OnServiceReady()
	{
	}

	/****************************
	*  OpenAI service methods  *
	****************************/

	public OpenAiRequest QueueChatCompletion(ChatCompletionRequest request)
	{
		LoggerManager.LogDebug("Queuing chat completion request", "", "request", request);

		var req = CreateRequestObject<OpenAiChatCompletionsRequest>(request);

		_requestQueue.Enqueue(req);

		return req;
	}

	public T CreateRequestObject<T>(RequestBase request) where T : OpenAiRequest, new()
	{
		T requestObj = new T() {
			Request = request,
			OpenAI = new OpenAI(_openAIConfig),
		};

		requestObj.OpenAI.SubscribeOwner<OpenAIResult>(_On_OpenAI_Result, isHighPriority:true);
		requestObj.OpenAI.SubscribeOwner<OpenAIError>(_On_OpenAI_Error, isHighPriority:true);
		requestObj.OpenAI.SubscribeOwner<OpenAIServerSentEvent>(_On_OpenAI_SSE, isHighPriority:true);
		requestObj.OpenAI.SubscribeOwner<OpenAIStreamingFinished>(_On_OpenAI_StreamFinished, isHighPriority:true);

		return requestObj;
	}

	public async void ProcessRequestQueue()
	{
		if (_requestQueue.TryPeek(out var item))
		{
			_requestQueue.Dequeue();

			ProcessQueuedRequest(item);
		}
	}

	public async void ProcessQueuedRequest(OpenAiRequest request)
	{
		LoggerManager.LogDebug("Processing queued request", "", "request", request.Request);
		LoggerManager.LogDebug("", "", "requestType", request.Request.GetType().Name);

		var requestObj = request.Request;

		_ongoingRequests.Add(request);

		// process chat completion requests
		if (request is OpenAiChatCompletionsRequest c)
		{
			ChatCompletionRequest req = (requestObj as ChatCompletionRequest);

			req.Stream = true;

			try
			{
				var result = await c.OpenAI.ChatCompletions(req);

				LoggerManager.LogDebug("OpenAI result", "", "result", result);
			}
			catch (System.Exception e)
			{
				LoggerManager.LogDebug("OpenAI request exception", "", "exception", e);

				throw;
			}
		}
	}

	public OpenAiRequest GetOngoingRequestFromInstance(OpenAI instance)
	{
		return _ongoingRequests.Where(x => x.OpenAI.Equals(instance)).FirstOrDefault();
	}

	public void ProcessChatCompletionsChunk(OpenAiChatCompletionsRequest requestObj, ChatCompletionChunkResult chunk)
	{
		// emit chunk event
		requestObj.Emit<OpenAIChatCompletionChunk>(e => e.Chunk = chunk);

		// process the token, used to emit the other events
		if (requestObj is OpenAiChatCompletionsRequest cr)
		{
			if (chunk.Choices[0].Delta != null)
			{
				string contentDelta = chunk.Choices[0].Delta.GetContent();

				// prepare the result object
				if (cr.Result.Choices.Count == 0)
				{
					cr.Result.Choices.Add(new());
					cr.Result.Choices[0].Message = new();

					cr.Result.Id = chunk.Id;
					cr.Result.Model = chunk.Model;
					cr.Result.Created = chunk.Created;
					cr.Result.SystemFingerprint = chunk.SystemFingerprint;
				}

				// add new token to text
				cr.Result.Choices[0].Message.Content += contentDelta;
				cr.Result.Choices[0].Message.Role = "assistant";

				LoggerManager.LogDebug("Current result object", "", "result", cr.Result);

				// check if chunk token ends with a newline and emit a Line
				// event
				if (contentDelta != null)
				{
					if (contentDelta.EndsWith("\n"))
					{
						string line = GetLastLineInString(cr.Result.Choices[0].Message.GetContent());

						ProcessChatCompletionLine(requestObj, line, chunk);
					}
				}
			}
			else
			{
				// set the finish reason
				cr.Result.Choices[0].FinishReason = chunk.Choices[0].FinishReason;
			}
		}

	}

	public void ProcessChatCompletionLine(OpenAiRequest requestObj, string line, ChatCompletionChunkResult chunk = null, bool isLastLine = false)
	{
		LoggerManager.LogDebug("New line detected", "", "line", line);

		requestObj.Emit<OpenAIChatCompletionLine>(e => {
				e.Text = line;
				e.Chunk = chunk;
				e.IsLast = isLastLine;
			});
	}

	public string GetLastLineInString(string content)
	{
		var lines = content.Split("\n");

		LoggerManager.LogDebug("Lines", "", "lines", lines);

		return lines.Last(x => x.Length > 0);
	}

	public string GetLastSentenceInString(string content)
	{
		var sentences = Regex.Split(content, @"(?<=[\.!\?])\s+");

		LoggerManager.LogDebug("Sentence", "", "sentences", sentences);

		return sentences.Last();
	}

	public void ProcessChatCompletionsStreamFinished(OpenAiRequest requestObj)
	{
		// emit an empty result since that's what we have when streaming is
		// enabled
		OpenAiChatCompletionsRequest req = (requestObj as OpenAiChatCompletionsRequest);

		if (req.Result.Choices.Count > 0)
		{
			// get the end of the string if there's no new line as the line
			string line = GetLastLineInString(req.Result.Choices[0].Message.GetContent());
			ProcessChatCompletionLine(requestObj, line, isLastLine:true);

			requestObj.Emit<OpenAIChatCompletionResult>(e => e.Result = req.Result);
		}

		_ongoingRequests.Remove(requestObj);
	}

	public void ProcessChatCompletionsResult(OpenAiRequest requestObj, ChatCompletionResult result)
	{
		// with streaming enabled this result would be empty too
		requestObj.Emit<OpenAIChatCompletionResult>(e => e.Result = (requestObj as OpenAiChatCompletionsRequest).Result);

		_ongoingRequests.Remove(requestObj);
	}

	public void ProcessChatCompletionsError(OpenAiRequest requestObj, ErrorResult result)
	{
		// emit the returned error object
		requestObj.Emit<OpenAIChatCompletionError>(e => e.Error = result);

		_ongoingRequests.Remove(requestObj);
	}

	/*****************************
	*  OpenAI callback methods  *
	*****************************/

	public void _On_OpenAI_Result(OpenAIResult e)
	{
		// skip empty results, likely means it's either SSE or an error
		if (e.Result == null)
		{
			return;
		}

		LoggerManager.LogDebug("OpenAI result event", "", "e", e.Result);

		OpenAiRequest request = GetOngoingRequestFromInstance((e.Owner as OpenAI));
		
		if (request == null)
		{
			LoggerManager.LogDebug("Unable to find request for instance", "", "event", "result");
		}
		else
		{
			ProcessChatCompletionsResult(request, (e.Result as ChatCompletionResult));
		}
	}
	
	public void _On_OpenAI_Error(OpenAIError e)
	{
		LoggerManager.LogError("OpenAI error event", "", "e", e.Error);

		OpenAiRequest request = GetOngoingRequestFromInstance((e.Owner as OpenAI));
		
		if (request == null)
		{
			LoggerManager.LogDebug("Unable to find request for instance", "", "event", "error");
		}
		else
		{
			ProcessChatCompletionsError(request, e.Error);
		}
	}

	public void _On_OpenAI_SSE(OpenAIServerSentEvent e)
	{
		// skip empty chunk sent on end
		if (e.Chunk == null)
		{
			return;
		}

		LoggerManager.LogDebug("OpenAI SSE event", "", "e", e.Chunk);

		OpenAiRequest request = GetOngoingRequestFromInstance((e.Owner as OpenAI));
		
		if (request == null)
		{
			LoggerManager.LogDebug("Unable to find request for instance", "", "event", "SSE");
		}
		else
		{
			ProcessChatCompletionsChunk((request as OpenAiChatCompletionsRequest), e.Chunk);
		}
	}

	public void _On_OpenAI_StreamFinished(OpenAIStreamingFinished e)
	{
		LoggerManager.LogDebug("OpenAI streaming finished event");

		OpenAiRequest request = GetOngoingRequestFromInstance((e.Owner as OpenAI));
		
		if (request == null)
		{
			LoggerManager.LogDebug("Unable to find request for instance", "", "event", "SSE finished");
		}
		else
		{
			ProcessChatCompletionsStreamFinished(request);
		}
	}
}

public partial class OpenAiRequest
{
	public RequestBase Request { get; set; }
	public OpenAI OpenAI { get; set; }
}
public partial class OpenAiChatCompletionsRequest : OpenAiRequest
{
	public new ChatCompletionRequest Request { get; set; }
	public ChatCompletionResult Result { get; set; } = new();
}
