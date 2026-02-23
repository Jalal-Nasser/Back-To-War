using System;
using UnityEngine;

namespace Back2War.Core.Commands
{
    /// <summary>
    /// Deterministic command payload model.
    /// - No Unity types in payload fields.
    /// - SelectedEntityIds must be sorted ascending and unique.
    /// </summary>
    public enum CommandType : byte
    {
        Move = 1,
        Attack = 2
    }

    public enum TargetKind : byte
    {
        Position = 1,
        Entity = 2
    }

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

        public static QuantizedPos2 FromWorldXZ(Vector3 world)
        {
            int tileX = (int)Math.Floor(world.x);
            int tileY = (int)Math.Floor(world.z);

            int xMt = checked(tileX * 1000 + 500);
            int yMt = checked(tileY * 1000 + 500);
            return new QuantizedPos2(xMt, yMt);
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
    }
}
