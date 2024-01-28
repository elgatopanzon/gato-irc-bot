/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : GatoConfig
 * @created     : Monday Jan 22, 2024 22:35:00 CST
 */

namespace GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

public partial class GatoConfig : VConfig
{
	internal readonly VValue<Dictionary<string, List<string>>> _replyWithoutHighlight;

	public Dictionary<string, List<string>> ReplyWithoutHighlight
	{
		get { return _replyWithoutHighlight.Value; }
		set { _replyWithoutHighlight.Value = value; }
	}

	internal readonly VValue<bool> _streamingLines;

	public bool StreamingLines
	{
		get { return _streamingLines.Value; }
		set { _streamingLines.Value = value; }
	}

	internal readonly VValue<string> _isTypingSuffix;

	public string IsTypingSuffix
	{
		get { return _isTypingSuffix.Value; }
		set { _isTypingSuffix.Value = value; }
	}

	internal readonly VValue<string> _modelProfileId;

	public string ModelProfileId
	{
		get { return _modelProfileId.Value; }
		set { _modelProfileId.Value = value; }
	}

	internal readonly VValue<Dictionary<string, ModelProfile>> _modelProfiles;

	public Dictionary<string, ModelProfile> ModelProfiles
	{
		get { return _modelProfiles.Value; }
		set { _modelProfiles.Value = value; }
	}

	internal ModelProfile ModelProfile
	{
		get { 
			return ModelProfiles[ModelProfileId];
		}
	}

	internal readonly VValue<List<string>> _defaultSystemPrompts;

	public List<string> DefaultSystemPrompts
	{
		get { return _defaultSystemPrompts.Value; }
		set { _defaultSystemPrompts.Value = value; }
	}

	internal readonly VValue<bool> _stripUnfinishedSentences;

	public bool StripUnfinishedSentences
	{
		get { return _stripUnfinishedSentences.Value; }
		set { _stripUnfinishedSentences.Value = value; }
	}

	public GatoConfig()
	{
		_replyWithoutHighlight = AddValidatedValue<Dictionary<string, List<string>>>(this)
		    .Default(new Dictionary<string, List<string>>())
		    .ChangeEventsEnabled();

		_streamingLines = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();

		_isTypingSuffix = AddValidatedValue<string>(this)
		    .Default(" ...🖉")
		    .ChangeEventsEnabled();

		_modelProfileId = AddValidatedValue<string>(this)
		    .Default("default")
		    .ChangeEventsEnabled();

		_modelProfiles = AddValidatedValue<Dictionary<string, ModelProfile>>(this)
		    .Default(new Dictionary<string, ModelProfile>() {
				{ "default", new() },
		    	})
		    .ChangeEventsEnabled();

		_defaultSystemPrompts = AddValidatedValue<List<string>>(this)
		    .Default(new List<string>() {
				"You are an IRC chat bot"
		    })
		    .ChangeEventsEnabled();

		_stripUnfinishedSentences = AddValidatedValue<bool>(this)
		    .Default(true)
		    .ChangeEventsEnabled();
	}
}

public partial class ModelProfile : VConfig
{
	internal readonly VNative<InferenceParams> _inference;

	public InferenceParams Inference
	{
		get { return _inference.Value; }
		set { _inference.Value = value; }
	}

	internal readonly VValue<int> _historyTokenSize;

	public int HistoryTokenSize
	{
		get { return _historyTokenSize.Value; }
		set { _historyTokenSize.Value = value; }
	}

	internal readonly VValue<List<string>> _systemPrompts;

	public List<string> SystemPrompts
	{
		get { return _systemPrompts.Value; }
		set { _systemPrompts.Value = value; }
	}

	public ModelProfile()
	{
		_inference = AddValidatedNative<InferenceParams>(this)
		    .Default(new InferenceParams())
		    .ChangeEventsEnabled();

		_historyTokenSize = AddValidatedValue<int>(this)
		    .Default(2048)
		    .ChangeEventsEnabled();

		_systemPrompts = AddValidatedValue<List<string>>(this)
		    .Default(new List<string>() {
		    })
		    .ChangeEventsEnabled();
	}
}

public partial class InferenceParams : VConfig
{
	internal readonly VValue<string> _model;

	public string Model
	{
		get { return _model.Value; }
		set { _model.Value = value; }
	}

	internal readonly VValue<double> _frequencyPenalty;

	public double FrequencyPenalty
	{
		get { return _frequencyPenalty.Value; }
		set { _frequencyPenalty.Value = value; }
	}

	internal readonly VValue<int?> _maxTokens;

	public int? MaxTokens
	{
		get { return _maxTokens.Value; }
		set { _maxTokens.Value = value; }
	}

	internal readonly VValue<double> _presencePenalty;

	public double PresencePenalty
	{
		get { return _presencePenalty.Value; }
		set { _presencePenalty.Value = value; }
	}

	internal readonly VValue<int?> _seed;

	public int? Seed
	{
		get { return _seed.Value; }
		set { _seed.Value = value; }
	}

	internal readonly VValue<double> _temperature;

	public double Temperature
	{
		get { return _temperature.Value; }
		set { _temperature.Value = value; }
	}

	internal readonly VValue<double> _topP;

	public double TopP
	{
		get { return _topP.Value; }
		set { _topP.Value = value; }
	}


	public InferenceParams() 
	{
		_model = AddValidatedValue<string>(this)
		    .Default("gpt-3.5-turbo")
		    .ChangeEventsEnabled();

		_frequencyPenalty = AddValidatedValue<double>(this)
		    .Default(0)
		    .ChangeEventsEnabled();

		_maxTokens = AddValidatedValue<int?>(this)
		    .Default(300)
		    .ChangeEventsEnabled();

		_presencePenalty = AddValidatedValue<double>(this)
		    .Default(0)
		    .ChangeEventsEnabled();

		_seed = AddValidatedValue<int?>(this)
		    .Default(null)
		    .ChangeEventsEnabled();
	
		_temperature = AddValidatedValue<double>(this)
		    .Default(1)
		    .ChangeEventsEnabled();

		_topP = AddValidatedValue<double>(this)
		    .Default(1)
		    .ChangeEventsEnabled();
	}
}
