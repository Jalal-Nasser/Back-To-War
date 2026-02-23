using Back2War.SimCore.Match;

namespace Back2War.SimCore.Hashing
{
    public static class MatchStateHasher
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static ulong ComputeHash(MatchState s)
        {
            if (s == null)
            {
                return 0UL;
            }

            ulong hash = FnvOffsetBasis;

            hash = MixByte(hash, (byte)s.Lifecycle);
            hash = MixUInt32(hash, s.Tick);

            for (int i = 0; i < s.Players.Length; i++)
            {
                PlayerMatchState p = s.Players[i];
                hash = MixByte(hash, p.PlayerId);
                hash = MixByte(hash, (byte)p.Outcome);
                hash = MixUInt32(hash, p.DefeatTick);
            }

            hash = MixUInt32(hash, s.Diplomacy.Version);

            var state = s.Diplomacy.StateArray;
            for (int i = 0; i < state.Length; i++)
            {
                hash = MixByte(hash, (byte)state[i]);
            }

            var flags = s.Diplomacy.FlagsArray;
            for (int i = 0; i < flags.Length; i++)
            {
                hash = MixByte(hash, (byte)flags[i]);
            }

            hash = MixByte(hash, s.Result.HasValue ? (byte)1 : (byte)0);
            if (s.Result.HasValue)
            {
                VictoryResult result = s.Result.Value;
                hash = MixUInt32(hash, result.WinTick);
                hash = MixByte(hash, result.WinnerPlayerId);
                hash = MixByte(hash, result.IsAllianceWin ? (byte)1 : (byte)0);
                hash = MixUInt32(hash, result.WinnerMaskLo);
            }

            return hash;
        }

        private static ulong MixUInt32(ulong hash, uint value)
        {
            hash = MixByte(hash, (byte)(value & 0xFF));
            hash = MixByte(hash, (byte)((value >> 8) & 0xFF));
            hash = MixByte(hash, (byte)((value >> 16) & 0xFF));
            hash = MixByte(hash, (byte)((value >> 24) & 0xFF));
            return hash;
        }

        private static ulong MixByte(ulong hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                hash *= FnvPrime;
                return hash;
            }
        }
    }
}
