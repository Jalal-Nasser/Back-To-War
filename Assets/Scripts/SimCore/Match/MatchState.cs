using System;
using System.Collections.Generic;
using Back2War.SimCore.Commands;
using Back2War.SimCore.Diplomacy;

namespace Back2War.SimCore.Match
{
    public enum MatchLifecycle : byte
    {
        Created = 0,
        LobbyOpen = 1,
        Running = 2,
        Resolved = 3,
        Closed = 4,
    }

    public enum OutcomeState : byte
    {
        Active = 0,
        Defeated_Eliminated = 1,
        Defeated_Surrendered = 2,
        Defeated_DisconnectTimeout = 3,
    }

    public struct PlayerMatchState
    {
        public byte PlayerId;
        public OutcomeState Outcome;
        public uint DefeatTick;
    }

    public sealed class MatchState
    {
        public MatchState(int playerCount)
        {
            if (playerCount <= 0 || playerCount > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            Lifecycle = MatchLifecycle.Created;
            Tick = 0;
            Players = new PlayerMatchState[playerCount];
            for (byte i = 0; i < playerCount; i++)
            {
                Players[i] = new PlayerMatchState
                {
                    PlayerId = i,
                    Outcome = OutcomeState.Active,
                    DefeatTick = 0,
                };
            }

            Diplomacy = new DiplomacyMatrix(playerCount);
            DiplomacyEvents = new List<DiplomacyEvent>();
            CommandsByTick = new Dictionary<uint, List<SimCommand>>();
            Result = null;
        }

        public MatchLifecycle Lifecycle;
        public uint Tick;
        public PlayerMatchState[] Players;
        public DiplomacyMatrix Diplomacy;
        public List<DiplomacyEvent> DiplomacyEvents;
        public Dictionary<uint, List<SimCommand>> CommandsByTick;
        public VictoryResult? Result;
    }
}
