using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public class V2Ghost : IGhost
{
    private readonly ulong _steamId;
    private readonly int _soapboxId;
    private readonly int _hatId;
    private readonly int _colorId;
    private readonly List<Frame> _frames;

    public V2Ghost(ulong steamId, int soapboxId, int hatId, int colorId, List<Frame> frames)
    {
        _steamId = steamId;
        _soapboxId = soapboxId;
        _hatId = hatId;
        _colorId = colorId;
        _frames = frames;
    }

    public int CurrentFrameIndex { get; private set; }

    public Color Color { get; }

    public void Initialize(GhostData ghost)
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
