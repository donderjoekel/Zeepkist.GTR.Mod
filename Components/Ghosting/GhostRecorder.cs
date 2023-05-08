using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Steamworks;
using TNRD.Zeepkist.GTR.Mod.Patches;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting;

public class GhostRecorder : MonoBehaviourWithLogging
{
    public static event Action<string> GhostRecorded;

    [Flags]
    private enum Flags : byte
    {
        None = 0,
        ArmsUp = 1 << 0,
        Braking = 1 << 1
    }

    private class Frame
    {
        public float Time { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float Steering { get; set; }
        public bool ArmsUp { get; set; }
        public bool Braking { get; set; }
    }

    private bool saveFrame;
    private bool hasStarted;
    private SetupCar setupCar;
    private ReadyToReset readyToReset;

    private readonly List<Frame> frames = new List<Frame>();

    protected override void Awake()
    {
        base.Awake();
        GameMaster_ReleaseTheZeepkists.ReleaseTheZeepkists += OnReleaseTheZeepkists;
        GameMaster_CrossedFinishOnline.CrossedFinishOnline += OnCrossedFinishOnline;
    }

    private void OnReleaseTheZeepkists()
    {
        frames.Clear();
        hasStarted = true;

        setupCar = PlayerManager.Instance.currentMaster.carSetups.FirstOrDefault();
        if (setupCar == null)
            Logger.LogError("We're trying to log a ghost but there's no car available!");

        readyToReset = PlayerManager.Instance.currentMaster.PlayersReady.FirstOrDefault();
        if (readyToReset == null)
            Logger.LogError("We're trying to log a ghost but there's no car available!");
    }

    private void OnCrossedFinishOnline()
    {
        const byte precision = 60;

        hasStarted = false;

        string b64;

        using (MemoryStream stream = new MemoryStream())
        {
            using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Compress))
            {
                using (BinaryWriter writer = new BinaryWriter(gZipStream))
                {
                    writer.Write(4); // Version
                    writer.Write(SteamClient.SteamId.Value);

                    writer.Write(PlayerManager.Instance.avontuurSoapbox.GetCompleteID());
                    writer.Write(PlayerManager.Instance.avontuurHat.GetCompleteID());
                    writer.Write(PlayerManager.Instance.avontuurColor.GetCompleteID());

                    writer.Write(precision);
                    // Add one because we also write the last frame as an reset frame
                    writer.Write(frames.Count + 1);

                    Frame lastFrame = null;

                    for (int i = 0; i < frames.Count; i++)
                    {
                        Frame frame = frames[i];
                        WriteFrame(frame, i % precision == 0, lastFrame, writer);
                        lastFrame = frame;
                    }

                    // Write the last frame as an reset frame
                    WriteFrame(frames.Last(), true, null, writer);
                }
            }

            byte[] buffer = stream.ToArray();
            b64 = Convert.ToBase64String(buffer);
        }

        GhostRecorded?.Invoke(b64);
    }

    private void WriteFrame(Frame frame, bool isResetFrame, Frame lastFrame, BinaryWriter writer)
    {
        writer.Write(frame.Time);

        if (isResetFrame)
        {
            writer.Write(frame.Position.x);
            writer.Write(frame.Position.y);
            writer.Write(frame.Position.z);
        }
        else
        {
            Vector3 deltaPosition = frame.Position - lastFrame.Position;

            writer.Write(FloatToShort(deltaPosition.x));
            writer.Write(FloatToShort(deltaPosition.y));
            writer.Write(FloatToShort(deltaPosition.z));
        }

        writer.Write(FloatToShort(frame.Rotation.x, 30000));
        writer.Write(FloatToShort(frame.Rotation.y, 30000));
        writer.Write(FloatToShort(frame.Rotation.z, 30000));
        writer.Write(FloatToShort(frame.Rotation.w, 30000));

        byte steering = MapFloatToByte(frame.Steering);
        writer.Write(steering);

        Flags flags = Flags.None;
        if (frame.ArmsUp) flags |= Flags.ArmsUp;
        if (frame.Braking) flags |= Flags.Braking;
        writer.Write((byte)flags);
    }

    // Method that converts a float to a short
    private short FloatToShort(float value, int scale = 10000)
    {
        float scaled = value * scale;
        if (scaled > short.MaxValue)
            return short.MaxValue;
        if (scaled < short.MinValue)
            return short.MinValue;
        return (short)scaled;
    }

    private void FixedUpdate()
    {
        if (!hasStarted)
            return;

        if (setupCar == null || readyToReset == null)
            return;

        saveFrame = !saveFrame;
        if (!saveFrame)
            return;

        Transform carTransform = setupCar.transform;
        float time = readyToReset.ticker.what_ticker;
        Vector3 position = carTransform.position;
        Quaternion rotation = carTransform.rotation;
        New_ControlCar cc = setupCar.cc;

        frames.Add(new Frame()
        {
            Time = time,
            Position = position,
            Rotation = rotation,
            Steering = cc.lerpedSteering,
            ArmsUp = cc.ArmsUpAction.buttonHeld,
            Braking = cc.BrakeAction.buttonHeld
        });
    }

    // Method that maps float from -1 to 1 to byte from 0 to 255
    private byte MapFloatToByte(float value)
    {
        return (byte)((value + 1) * 127.5f);
    }
}
