/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : Events
 * @created     : Friday Jan 26, 2024 12:31:36 CST
 */

namespace GatoIRCBot.Event;

using GodotEGP.AI.OpenAI;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class OpenAIServiceEvent : Event
{
	public RequestBase Request { get; set; }
}

public partial class OpenAITextCompletionEvent : OpenAIServiceEvent 
{
	public string Text { get; set; }
}
public partial class OpenAIChatCompletionChunk : OpenAITextCompletionEvent 
{
	public ChatCompletionChunkResult Chunk { get; set; }
}
public partial class OpenAIChatCompletionLine : OpenAIChatCompletionChunk 
{
	public bool IsLast { get; set; } = false;
}

public partial class OpenAIResultEvent : OpenAIServiceEvent 
{
	public ErrorResult Error { get; set; }
}
public partial class OpenAIChatCompletionResult : OpenAIResultEvent 
{
	public ChatCompletionResult Result { get; set; }
}
public partial class OpenAIResultError : OpenAIResultEvent 
{
}
public partial class OpenAIChatCompletionError : OpenAIChatCompletionResult 
{
}
