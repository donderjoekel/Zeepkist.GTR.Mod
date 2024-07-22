using System;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostPlayer
{
    public class GhostRemovedEventArgs : EventArgs
    {
        public int RecordId { get; }

        public GhostRemovedEventArgs(int recordId)
        {
            RecordId = recordId;
        }
    }

    public class GhostAddedEventArgs : EventArgs
    {
        public int RecordId { get; }
        public IGhost Ghost { get; }
        public GhostVisuals Visuals { get; }

        public GhostAddedEventArgs(int recordId, IGhost ghost, GhostVisuals visuals)
        {
            RecordId = recordId;
            Ghost = ghost;
            Visuals = visuals;
        }
    }
}
