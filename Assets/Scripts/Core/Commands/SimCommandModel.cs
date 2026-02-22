using System;

namespace Back2War.Core.Commands
{
    /// <summary>
    /// Determinism constraints:
    /// - Payload types use integers/enums only.
    /// - SelectedEntityIds must be sorted ascending and unique.
    /// - No UnityEngine types in serialized command payloads.
    /// </summary>
    public enum CommandType : byte
    {
        Move = 1,
        Attack = 2,
        AttackMove = 3,
        Stop = 4,
        Hold = 5,
        Patrol = 6,
        Gather = 7,
        Build = 8,
        Train = 9,
        Research = 10,
        SetRallyPoint = 11,
        SetAlliance = 12
    }

    public enum TargetKind : byte
    {
        None = 0,
        Entity = 1,
        Position = 2,
        Alliance = 3
    }

    public enum AllianceRelation : byte
    {
        Enemy = 0,
        Ally = 1,
        Neutral = 2
    }

    /// <summary>
    /// Position in milli-tiles. 1000 = 1 tile.
    /// Center snapped tile coordinates are tile*1000 + 500.
    /// </summary>
    [Serializable]
    public struct QuantizedPos2
    {
        public int XMt;
        public int YMt;

        public QuantizedPos2(int xMt, int yMt)
        {
            XMt = xMt;
            YMt = yMt;
        }
    }

    [Serializable]
    public struct AllianceTarget
    {
        public byte OtherPlayerId;
        public AllianceRelation Relation;
        public byte SharedFlags;

        public AllianceTarget(byte otherPlayerId, AllianceRelation relation, byte sharedFlags)
        {
            OtherPlayerId = otherPlayerId;
            Relation = relation;
            SharedFlags = sharedFlags;
        }
    }

    [Serializable]
    public struct SimCommand
    {
        public uint ApplyTick;
        public byte PlayerId;
        public uint CommandSeq;
        public CommandType Type;
        public bool Queued;

        public uint[] SelectedEntityIds;

        public TargetKind TargetKind;
        public uint TargetEntityId;
        public QuantizedPos2 TargetPos;
        public AllianceTarget Alliance;
    }

    [Serializable]
    public struct CommandBatch
    {
        public string MatchId;
        public uint ClientInputSeq;
        public int AckBundleTick;
        public SimCommand[] Commands;
    }
}
