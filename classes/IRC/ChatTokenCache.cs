/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : ChatTokenCache
 * @created     : Thursday Feb 01, 2024 12:29:11 CST
 */

namespace GatoIRCBot.IRC;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

using GodotEGP.AI.GatoGPT;

public partial class ChatTokenCache
{
	public Dictionary<string, Dictionary<string, List<TokenizedString>>> _tokenCache;

	public ChatTokenCache()
	{
		_tokenCache = new();
	}

	public Dictionary<string, List<TokenizedString>> InitCacheForGroup(string groupName)
	{
		if (!_tokenCache.TryGetValue(groupName, out var groupCache))
		{
			groupCache = new();
			_tokenCache[groupName] = groupCache;
		}

		return groupCache;
	}

	public void StoreCache(string groupName, string content, List<TokenizedString> tokens)
	{
		var cacheGroup = InitCacheForGroup(groupName);

		cacheGroup[content] = tokens;
	}

	public List<TokenizedString> GetCache(string groupName, string content)
	{
		var cacheGroup = InitCacheForGroup(groupName);

		if (cacheGroup.TryGetValue(content, out var tokens))
		{
			return tokens;
		}

		return null;
	}
}
