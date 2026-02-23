using System.Collections.Generic;

namespace Back2War.SimCore.Diplomacy
{
    public struct DiplomacyEvent
    {
        public uint ApplyTick;
        public uint EventId;
        public byte A;
        public byte B;
        public RelationState NewState;
        public RelationFlags NewFlags;
    }

    public static class DiplomacyEventOrder
    {
        public static readonly IComparer<DiplomacyEvent> StableComparer = new StableComparerImpl();

        private sealed class StableComparerImpl : IComparer<DiplomacyEvent>
        {
            public int Compare(DiplomacyEvent x, DiplomacyEvent y)
            {
                int compare = x.ApplyTick.CompareTo(y.ApplyTick);
                if (compare != 0)
                {
                    return compare;
                }

                return x.EventId.CompareTo(y.EventId);
            }
        }
    }
}
