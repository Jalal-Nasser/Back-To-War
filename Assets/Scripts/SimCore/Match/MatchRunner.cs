using System.Collections.Generic;
using Back2War.SimCore.Commands;
using Back2War.SimCore.Diplomacy;

namespace Back2War.SimCore.Match
{
    public static class MatchRunner
    {
        public static void StepOneTick(MatchState s)
        {
            if (s == null)
            {
                return;
            }

            if (s.Lifecycle != MatchLifecycle.Running)
            {
                return;
            }

            // Stage 0: setup (placeholder)

            // Stage 1: apply diplomacy events for this tick in deterministic order.
            if (s.DiplomacyEvents.Count > 1)
            {
                s.DiplomacyEvents.Sort(DiplomacyEventOrder.StableComparer);
            }

            int writeIndex = 0;
            int count = s.DiplomacyEvents.Count;
            for (int readIndex = 0; readIndex < count; readIndex++)
            {
                DiplomacyEvent evt = s.DiplomacyEvents[readIndex];
                if (evt.ApplyTick == s.Tick)
                {
                    s.Diplomacy.Apply(evt.A, evt.B, evt.NewState, evt.NewFlags);
                    continue;
                }

                s.DiplomacyEvents[writeIndex] = evt;
                writeIndex++;
            }

            if (writeIndex < count)
            {
                s.DiplomacyEvents.RemoveRange(writeIndex, count - writeIndex);
            }

            // Stage 2: process commands for this tick in deterministic order.
            List<SimCommand> commands;
            if (s.CommandsByTick.TryGetValue(s.Tick, out commands))
            {
                commands.Sort(SimCommandOrder.StableComparer);
                // Deterministic command ordering hook: gameplay effects are intentionally not implemented yet.
                s.CommandsByTick.Remove(s.Tick);
            }

            // Stage 3: deterministic outcome transitions (predicate hooks TODO).
            for (int i = 0; i < s.Players.Length; i++)
            {
                if (s.Players[i].Outcome != OutcomeState.Active)
                {
                    continue;
                }

                if (SurrenderedPredicateTodo(s, s.Players[i].PlayerId))
                {
                    SetDefeated(s, i, OutcomeState.Defeated_Surrendered);
                    continue;
                }

                if (EliminatedPredicateTodo(s, s.Players[i].PlayerId))
                {
                    SetDefeated(s, i, OutcomeState.Defeated_Eliminated);
                    continue;
                }

                if (DisconnectTimeoutPredicateTodo(s, s.Players[i].PlayerId))
                {
                    SetDefeated(s, i, OutcomeState.Defeated_DisconnectTimeout);
                }
            }

            // Stage 4: victory evaluation skeleton.
            if (!s.Result.HasValue)
            {
                if (!TryResolveLastPlayer(s))
                {
                    TryResolveLastAlliance(s);
                }
            }

            // Stage 5: commit.
            s.Tick++;
        }

        private static void SetDefeated(MatchState s, int playerIndex, OutcomeState newOutcome)
        {
            PlayerMatchState player = s.Players[playerIndex];
            player.Outcome = newOutcome;
            player.DefeatTick = s.Tick;
            s.Players[playerIndex] = player;
        }

        private static bool SurrenderedPredicateTodo(MatchState s, byte playerId)
        {
            // TODO: wire surrender events into match state.
            return false;
        }

        private static bool EliminatedPredicateTodo(MatchState s, byte playerId)
        {
            // TODO: wire elimination predicate from gameplay state.
            return false;
        }

        private static bool DisconnectTimeoutPredicateTodo(MatchState s, byte playerId)
        {
            // TODO: wire disconnect timeout state and rules.
            return false;
        }

        private static bool TryResolveLastPlayer(MatchState s)
        {
            int aliveCount = 0;
            byte alivePlayerId = 0;

            for (int i = 0; i < s.Players.Length; i++)
            {
                if (s.Players[i].Outcome != OutcomeState.Active)
                {
                    continue;
                }

                aliveCount++;
                alivePlayerId = s.Players[i].PlayerId;

                if (aliveCount > 1)
                {
                    break;
                }
            }

            if (aliveCount != 1)
            {
                return false;
            }

            uint mask = alivePlayerId < 32 ? (1u << alivePlayerId) : 0u;
            s.Result = new VictoryResult
            {
                WinTick = s.Tick,
                WinnerPlayerId = alivePlayerId,
                IsAllianceWin = false,
                WinnerMaskLo = mask,
            };
            s.Lifecycle = MatchLifecycle.Resolved;
            return true;
        }

        private static bool TryResolveLastAlliance(MatchState s)
        {
            int playerCount = s.Players.Length;
            bool[] active = new bool[playerCount];
            int activeCount = 0;

            for (int i = 0; i < playerCount; i++)
            {
                if (s.Players[i].Outcome == OutcomeState.Active)
                {
                    active[i] = true;
                    activeCount++;
                }
            }

            if (activeCount == 0)
            {
                return false;
            }

            bool[] visited = new bool[playerCount];
            int componentCount = 0;

            for (int i = 0; i < playerCount; i++)
            {
                if (!active[i] || visited[i])
                {
                    continue;
                }

                componentCount++;
                if (componentCount > 1)
                {
                    return false;
                }

                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    for (int neighbor = 0; neighbor < playerCount; neighbor++)
                    {
                        if (!active[neighbor] || visited[neighbor])
                        {
                            continue;
                        }

                        if (s.Diplomacy.GetState(current, neighbor) != RelationState.Ally)
                        {
                            continue;
                        }

                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (componentCount != 1)
            {
                return false;
            }

            uint winnerMask = 0u;
            for (int i = 0; i < playerCount && i < 32; i++)
            {
                if (active[i])
                {
                    winnerMask |= 1u << i;
                }
            }

            s.Result = new VictoryResult
            {
                WinTick = s.Tick,
                WinnerPlayerId = 255,
                IsAllianceWin = true,
                WinnerMaskLo = winnerMask,
            };
            s.Lifecycle = MatchLifecycle.Resolved;
            return true;
        }
    }
}
