using System;
using System.Collections.Generic;
using Back2War.Core.Commands;
using UnityEngine;

namespace Back2War.Core.Simulation
{
    /// <summary>
    /// Determinism constraints:
    /// - Commands are buffered by ApplyTick.
    /// - Per-tick execution order is stable: (PlayerId, CommandSeq, Type, FirstSelectedEntityId).
    /// - SelectedEntityIds are canonicalized to sorted unique arrays at enqueue.
    /// </summary>
    public sealed class CommandQueue : MonoBehaviour
    {
        [SerializeField] private bool debugLogs = true;

        private readonly SortedDictionary<uint, List<SimCommand>> _commandsByTick =
            new SortedDictionary<uint, List<SimCommand>>();

        public int BufferedTicks => _commandsByTick.Count;

        public void Enqueue(SimCommand command)
        {
            SimCommand canonical = Canonicalize(command);

            if (!_commandsByTick.TryGetValue(canonical.ApplyTick, out List<SimCommand> list))
            {
                list = new List<SimCommand>(16);
                _commandsByTick.Add(canonical.ApplyTick, list);
            }

            list.Add(canonical);
        }

        public void ProcessTick(uint localSimTick)
        {
            if (!_commandsByTick.TryGetValue(localSimTick, out List<SimCommand> list))
            {
                return;
            }

            list.Sort(CompareForExecution);

            for (int i = 0; i < list.Count; i++)
            {
                ApplyCommand(list[i], localSimTick);
            }

            _commandsByTick.Remove(localSimTick);
        }

        private void ApplyCommand(SimCommand command, uint localSimTick)
        {
            switch (command.Type)
            {
                case CommandType.Move:
                    if (debugLogs)
                    {
                        Debug.Log(
                            $"[CommandQueue] Tick {localSimTick}: MOVE p={command.PlayerId} seq={command.CommandSeq} " +
                            $"to=({command.TargetPos.XMt},{command.TargetPos.YMt}) units={command.SelectedEntityIds.Length}");
                    }
                    break;

                case CommandType.Attack:
                    if (debugLogs)
                    {
                        Debug.Log(
                            $"[CommandQueue] Tick {localSimTick}: ATTACK p={command.PlayerId} seq={command.CommandSeq} " +
                            $"target={command.TargetEntityId} units={command.SelectedEntityIds.Length}");
                    }
                    break;

                default:
                    if (debugLogs)
                    {
                        Debug.Log(
                            $"[CommandQueue] Tick {localSimTick}: {command.Type} (not implemented yet) p={command.PlayerId} seq={command.CommandSeq}");
                    }
                    break;
            }
        }

        private static int CompareForExecution(SimCommand a, SimCommand b)
        {
            int byPlayer = a.PlayerId.CompareTo(b.PlayerId);
            if (byPlayer != 0)
            {
                return byPlayer;
            }

            int bySeq = a.CommandSeq.CompareTo(b.CommandSeq);
            if (bySeq != 0)
            {
                return bySeq;
            }

            int byType = ((byte)a.Type).CompareTo((byte)b.Type);
            if (byType != 0)
            {
                return byType;
            }

            uint aFirst = FirstSelectedEntityId(a.SelectedEntityIds);
            uint bFirst = FirstSelectedEntityId(b.SelectedEntityIds);
            return aFirst.CompareTo(bFirst);
        }

        private static uint FirstSelectedEntityId(uint[] ids)
        {
            return ids != null && ids.Length > 0 ? ids[0] : uint.MaxValue;
        }

        private static SimCommand Canonicalize(SimCommand command)
        {
            SimCommand canonical = command;
            canonical.SelectedEntityIds = CanonicalizeSelectedEntityIds(command.SelectedEntityIds);
            return canonical;
        }

        private static uint[] CanonicalizeSelectedEntityIds(uint[] entityIds)
        {
            if (entityIds == null || entityIds.Length == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] sorted = (uint[])entityIds.Clone();
            Array.Sort(sorted);

            int write = 1;
            for (int read = 1; read < sorted.Length; read++)
            {
                if (sorted[read] != sorted[read - 1])
                {
                    sorted[write] = sorted[read];
                    write++;
                }
            }

            if (write == sorted.Length)
            {
                return sorted;
            }

            uint[] unique = new uint[write];
            Array.Copy(sorted, unique, write);
            return unique;
        }
    }
}
