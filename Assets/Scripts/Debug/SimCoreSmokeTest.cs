using System.Collections.Generic;
using Back2War.SimCore.Commands;
using Back2War.SimCore.Diplomacy;
using Back2War.SimCore.Hashing;
using Back2War.SimCore.Match;
using UnityEngine;

namespace Back2War.SimCore.Debug
{
    public sealed class SimCoreSmokeTest : MonoBehaviour
    {
        private void Start()
        {
            MatchState state = new MatchState(3)
            {
                Lifecycle = MatchLifecycle.Running,
                Tick = 0,
            };

            // Explicit deterministic initialization: self ally, non-self enemy.
            for (byte a = 0; a < state.Players.Length; a++)
            {
                for (byte b = 0; b < state.Players.Length; b++)
                {
                    RelationState relation = a == b ? RelationState.Ally : RelationState.Enemy;
                    state.Diplomacy.Apply(a, b, relation, RelationFlags.None);
                }
            }

            state.DiplomacyEvents.Add(new DiplomacyEvent
            {
                ApplyTick = 5,
                EventId = 1,
                A = 0,
                B = 1,
                NewState = RelationState.Ally,
                NewFlags = RelationFlags.None,
            });

            state.CommandsByTick[3] = new List<SimCommand>
            {
                new SimCommand
                {
                    ApplyTick = 3,
                    PlayerId = 1,
                    CommandSeq = 2,
                    Type = CommandType.Attack,
                    Queued = false,
                    SelectedEntityIds = new uint[] { 21 },
                    TargetKind = TargetKind.Entity,
                    TargetEntityId = 99,
                    TargetPos = default(QuantizedPos2),
                },
                new SimCommand
                {
                    ApplyTick = 3,
                    PlayerId = 0,
                    CommandSeq = 1,
                    Type = CommandType.Move,
                    Queued = false,
                    SelectedEntityIds = new uint[] { 10, 20 },
                    TargetKind = TargetKind.Position,
                    TargetEntityId = 0,
                    TargetPos = QuantizedPos2.FromTile(4, 7),
                },
            };

            for (int i = 0; i < 20; i++)
            {
                MatchRunner.StepOneTick(state);
                if (state.Tick % 5 == 0)
                {
                    ulong hash = MatchStateHasher.ComputeHash(state);
                    UnityEngine.Debug.Log($"[SimCoreSmokeTest] tick={state.Tick} hash=0x{hash:X16}");
                }
            }
        }
    }
}
