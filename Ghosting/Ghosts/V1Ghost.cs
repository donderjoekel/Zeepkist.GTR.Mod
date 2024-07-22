using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public class V1Ghost : IGhost
{
    private readonly List<Frame> _frames;

    public V1Ghost(List<Frame> frames)
    {
        _frames = frames;
    }

    public int CurrentFrameIndex { get; private set; }

    public void Initialize(GhostVisuals ghost)
    {
        throw new System.NotImplementedException();
    }

    public void ApplyCosmetics(string steamName)
    {
        throw new System.NotImplementedException();
    }

    public void Start()
    {
        throw new System.NotImplementedException();
    }

    public void Stop()
    {
        throw new System.NotImplementedException();
    }

    public void Update()
    {
        throw new System.NotImplementedException();
    }

    public void FixedUpdate()
    {
        throw new System.NotImplementedException();
    }

    public void Update(NetworkedZeepkistGhost ghost)
    {
        throw new System.NotImplementedException();
    }

    public void IncrementFrame()
    {
        SetFrame(CurrentFrameIndex + 1);
    }

    public void SetFrame(int index)
    {
        CurrentFrameIndex = Mathf.Clamp(index, 0, _frames.Count - 1);
    }

    public class Frame
    {
        public Frame(float time, Vector3 position, Quaternion rotation)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
        }

        public float Time { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
    }
}
