using System;
using System.Collections.Generic;
using System.Text;
using Back2War.Core.Commands;
using UnityEngine;

namespace Back2War.Core.Simulation
{
    /// <summary>
    /// Deterministic per-tick command queue.
    /// </summary>
    public sealed class CommandQueue : MonoBehaviour
    {
        private readonly SortedDictionary<uint, List<SimCommand>> _byTick =
            new SortedDictionary<uint, List<SimCommand>>();

        private readonly StringBuilder _sb = new StringBuilder(128);

        public void Enqueue(SimCommand cmd)
        {
            cmd.SelectedEntityIds = Canonicalize(cmd.SelectedEntityIds);

            if (!_byTick.TryGetValue(cmd.ApplyTick, out List<SimCommand> list))
            {
                list = new List<SimCommand>(8);
                _byTick.Add(cmd.ApplyTick, list);
            }

            list.Add(cmd);
        }

        public void ProcessTick(uint tick)
        {
            if (!_byTick.TryGetValue(tick, out List<SimCommand> list))
            {
                return;
            }

            list.Sort(CompareWithinTick);

            for (int i = 0; i < list.Count; i++)
            {
                SimCommand cmd = list[i];
                string target;

                if (cmd.TargetKind == TargetKind.Entity)
                {
                    target = "TargetEntityId=" + cmd.TargetEntityId;
                }
                else
                {
                    target = "TargetPos=(" + cmd.TargetPos.XMt + "," + cmd.TargetPos.YMt + ")";
                }

                Debug.Log("[TICK " + tick + "] APPLY " + cmd.Type +
                          " queued=" + (cmd.Queued ? "1" : "0") +
                          " sel=" + BuildIds(cmd.SelectedEntityIds) +
                          " " + target);
            }

            _byTick.Remove(tick);
        }

        private string BuildIds(uint[] ids)
        {
            _sb.Clear();
            _sb.Append('[');
            if (ids != null)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    if (i > 0)
                    {
                        _sb.Append(',');
                    }

                    _sb.Append(ids[i]);
                }
            }

            _sb.Append(']');
            return _sb.ToString();
        }

        private static int CompareWithinTick(SimCommand a, SimCommand b)
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

            int byType = ((int)a.Type).CompareTo((int)b.Type);
            if (byType != 0)
            {
                return byType;
            }

            uint aFirst = FirstSelected(a.SelectedEntityIds);
            uint bFirst = FirstSelected(b.SelectedEntityIds);
            return aFirst.CompareTo(bFirst);
        }

        private static uint FirstSelected(uint[] ids)
        {
            return ids != null && ids.Length > 0 ? ids[0] : uint.MaxValue;
        }

        private static uint[] Canonicalize(uint[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] sorted = (uint[])ids.Clone();
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
