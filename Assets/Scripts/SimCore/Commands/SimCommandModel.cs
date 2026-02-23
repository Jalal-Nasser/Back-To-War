using System.Collections.Generic;
using Back2War.SimCore.Core;

namespace Back2War.SimCore.Commands
{
    public enum CommandType : byte
    {
        Move = 1,
        Attack = 2,
        Build = 3,
    }

    public enum TargetKind : byte
    {
        Position = 1,
        Entity = 2,
    }

    public struct QuantizedPos2
    {
        public int XMt;
        public int YMt;

        public QuantizedPos2(int xMt, int yMt)
        {
            XMt = xMt;
            YMt = yMt;
        }

        public static QuantizedPos2 FromTile(int tileX, int tileY)
        {
            return new QuantizedPos2((tileX * 1000) + 500, (tileY * 1000) + 500);
        }
    }

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

    public static class SimCommandOrder
    {
        public static readonly IComparer<SimCommand> StableComparer = new StableComparerImpl();

        private sealed class StableComparerImpl : IComparer<SimCommand>
        {
            public int Compare(SimCommand x, SimCommand y)
            {
                int compare = x.PlayerId.CompareTo(y.PlayerId);
                if (compare != 0)
                {
                    return compare;
                }

                compare = x.CommandSeq.CompareTo(y.CommandSeq);
                if (compare != 0)
                {
                    return compare;
                }

                compare = x.Type.CompareTo(y.Type);
                if (compare != 0)
                {
                    return compare;
                }

                uint xFirst = Deterministic.FirstOrZero(x.SelectedEntityIds);
                uint yFirst = Deterministic.FirstOrZero(y.SelectedEntityIds);
                return xFirst.CompareTo(yFirst);
            }
        }
    }
}
