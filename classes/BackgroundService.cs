/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : BackgroundService
 * @created     : Sunday Jan 21, 2024 18:34:53 CST
 */

namespace GodotEGPNonGame.ServiceWorkers;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;
using GodotEGP.Threading;

using System.ComponentModel;

/// <summary>
/// ServiceWorker using BackgroundJob instead of AspCore BackgroundService
/// </summary>
public partial class BackgroundService : BackgroundJob
{
	private CancellationToken _cancellationToken { get; set; }

	public BackgroundService()
	{
	}

	protected virtual async Task ExecuteAsync(CancellationToken cancellationToken)
	{
	}

	public virtual async Task StartAsync(CancellationToken cancellationToken)
	{
		_cancellationToken = cancellationToken;

		// call BackgroundJob.Run() to start the threaded worker
		base.Run();
	}

	public override void DoWork(object sender, DoWorkEventArgs e)
	{
		ExecuteAsync(_cancellationToken);
	}
}

