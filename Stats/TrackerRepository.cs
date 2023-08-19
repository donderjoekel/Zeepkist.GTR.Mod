using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.Mod.Stats.Trackers;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace TNRD.Zeepkist.GTR.Mod.Stats;

internal class TrackerRepository : MonoBehaviour
{
    private readonly ManualLogSource logger = Logger.CreateLogSource(nameof(TrackerRepository));

    private readonly List<ITracker> trackers = new();
    private readonly List<ITickable> tickables = new();
    private readonly List<IDisposable> disposables = new();

    public static TrackerRepository Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        foreach (IDisposable disposable in disposables)
        {
            disposable.Dispose();
        }
    }

    private void Start()
    {
        Register(new ArmsUpDistanceTracker());
        Register(new ArmsUpTimeTracker());
        Register(new BrakingDistanceTracker());
        Register(new BrakingTimeTracker());
        Register(new CrashTracker());
        Register(new GroundedDistanceTracker());
        Register(new GroundedTimeTracker());
        Register(new GroundTypeDistanceTracker());
        Register(new GroundTypeTimeTracker());
        Register(new InAirDistanceTracker());
        Register(new InAirTimeTracker());
        Register(new OnWheelsDistanceTracker());
        Register(new OnWheelsTimeTracker());
        Register(new RagdollDistanceTracker());
        Register(new RagdollTimeTracker());
        Register(new WithWheelsDistanceTracker());
        Register(new WithWheelsTimeTracker());

        Register(new CheckpointsCrossedTracker());
        Register(new TimesFinishedTracker());
        Register(new TimesStartedTracker());
        Register(new WheelsBrokenTracker());

        ResetTrackers();
    }

    public void Register(ITracker tracker)
    {
        trackers.Add(tracker);
        if (tracker is ITickable tickable)
            tickables.Add(tickable);
        if (tracker is IDisposable disposable)
            disposables.Add(disposable);
    }

    private void ResetTrackers()
    {
        foreach (ITracker tracker in trackers)
        {
            tracker.Reset();
        }
    }

    private void Update()
    {
        foreach (ITickable tickable in tickables)
        {
            tickable.Tick();
        }
    }

    public UsersUpdateStatsRequestDTO CalculateStats()
    {
        UsersUpdateStatsRequestDTO model = new();

        foreach (ITracker tracker in trackers.OrderBy(x => x.GetType().Name))
        {
            tracker.ApplyStats(model);
        }

        ResetTrackers();

        return model;
    }
}
