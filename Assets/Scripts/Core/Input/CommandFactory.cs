using System;
using System.Collections.Generic;
using Back2War.Core.Commands;
using Back2War.Core.Selection;
using Back2War.Core.Simulation;
using UnityEngine;

namespace Back2War.Core.Input
{
    /// <summary>
    /// Deterministic command creation helpers.
    /// </summary>
    public static class CommandFactory
    {
        public static uint[] Canonicalize(List<uint> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] sorted = ids.ToArray();
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

        public static SimCommand CreateMove(
            SelectionController selection,
            SimClock clock,
            byte playerId,
            ref uint seq,
            bool queued,
            Vector3 worldPoint)
        {
            return new SimCommand
            {
                ApplyTick = clock.ComputeApplyTick(),
                PlayerId = playerId,
                CommandSeq = ++seq,
                Type = CommandType.Move,
                Queued = queued,
                SelectedEntityIds = SelectionToCanonicalIds(selection),
                TargetKind = TargetKind.Position,
                TargetEntityId = 0,
                TargetPos = QuantizedPos2.FromWorldXZ(worldPoint)
            };
        }

        public static SimCommand CreateAttack(
            SelectionController selection,
            SimClock clock,
            byte playerId,
            ref uint seq,
            bool queued,
            uint targetEntityId)
        {
            return new SimCommand
            {
                ApplyTick = clock.ComputeApplyTick(),
                PlayerId = playerId,
                CommandSeq = ++seq,
                Type = CommandType.Attack,
                Queued = queued,
                SelectedEntityIds = SelectionToCanonicalIds(selection),
                TargetKind = TargetKind.Entity,
                TargetEntityId = targetEntityId,
                TargetPos = default
            };
        }

        private static uint[] SelectionToCanonicalIds(SelectionController selection)
        {
            IReadOnlyList<uint> selected = selection != null ? selection.SelectedIdsSorted : null;
            if (selected == null || selected.Count == 0)
            {
                return Array.Empty<uint>();
            }

            var ids = new List<uint>(selected.Count);
            for (int i = 0; i < selected.Count; i++)
            {
                ids.Add(selected[i]);
            }

            return Canonicalize(ids);
        }
    }
}
