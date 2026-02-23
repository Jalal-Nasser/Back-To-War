using System;

namespace Back2War.SimCore.Diplomacy
{
    public enum RelationState : byte
    {
        Enemy = 0,
        Ally = 1,
        Neutral = 2,
    }

    [Flags]
    public enum RelationFlags : byte
    {
        None = 0,
        Breakable = 1 << 0,
        Pending = 1 << 1,
    }

    public sealed class DiplomacyMatrix
    {
        private readonly int n;
        private readonly RelationState[] state;
        private readonly RelationFlags[] flags;
        private uint version;

        public DiplomacyMatrix(int playerCount)
        {
            if (playerCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            n = playerCount;
            state = new RelationState[n * n];
            flags = new RelationFlags[n * n];

            for (int a = 0; a < n; a++)
            {
                for (int b = 0; b < n; b++)
                {
                    int index = IndexOf(a, b);
                    state[index] = a == b ? RelationState.Ally : RelationState.Enemy;
                    flags[index] = RelationFlags.None;
                }
            }
        }

        public int PlayerCount
        {
            get { return n; }
        }

        public uint Version
        {
            get { return version; }
        }

        public RelationState[] StateArray
        {
            get { return state; }
        }

        public RelationFlags[] FlagsArray
        {
            get { return flags; }
        }

        public int IndexOf(int a, int b)
        {
            if ((uint)a >= (uint)n)
            {
                throw new ArgumentOutOfRangeException(nameof(a));
            }

            if ((uint)b >= (uint)n)
            {
                throw new ArgumentOutOfRangeException(nameof(b));
            }

            return (a * n) + b;
        }

        public RelationState GetState(int a, int b)
        {
            return state[IndexOf(a, b)];
        }

        public RelationFlags GetFlags(int a, int b)
        {
            return flags[IndexOf(a, b)];
        }

        public void Apply(byte a, byte b, RelationState newState, RelationFlags newFlags)
        {
            int ai = a;
            int bi = b;

            IndexOf(ai, bi);

            RelationFlags sanitizedFlags = newState == RelationState.Ally ? newFlags : RelationFlags.None;

            if (ai == bi)
            {
                int self = IndexOf(ai, bi);
                state[self] = RelationState.Ally;
                flags[self] = RelationFlags.None;
                version++;
                return;
            }

            int ab = IndexOf(ai, bi);
            int ba = IndexOf(bi, ai);

            state[ab] = newState;
            state[ba] = newState;
            flags[ab] = sanitizedFlags;
            flags[ba] = sanitizedFlags;

            int aa = IndexOf(ai, ai);
            int bb = IndexOf(bi, bi);
            state[aa] = RelationState.Ally;
            state[bb] = RelationState.Ally;
            flags[aa] = RelationFlags.None;
            flags[bb] = RelationFlags.None;

            version++;
        }
    }
}
