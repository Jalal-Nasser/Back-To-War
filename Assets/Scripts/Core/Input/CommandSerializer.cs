using System;
using System.Collections.Generic;
using Back2War.Core.Commands;
using UnityEngine;

namespace Back2War.Core.Input
{
    /// <summary>
    /// Legacy serializer helpers retained for older prototype scripts.
    /// </summary>
    public static class CommandSerializer
    {
        public static QuantizedPos2 WorldToTileCenterQuantized(Vector3 worldPoint)
        {
            return QuantizedPos2.FromWorldXZ(worldPoint);
        }

        public static uint[] CanonicalizeEntityIds(List<uint> entityIds)
        {
            if (entityIds == null || entityIds.Count == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] sorted = entityIds.ToArray();
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

        public static SimCommand CreateMoveCommand(
            uint applyTick,
            byte playerId,
            uint commandSeq,
            List<uint> selectedEntityIds,
            Vector3 worldPoint,
            bool queued)
        {
            return new SimCommand
            {
                ApplyTick = applyTick,
                PlayerId = playerId,
                CommandSeq = commandSeq,
                Type = CommandType.Move,
                Queued = queued,
                SelectedEntityIds = CanonicalizeEntityIds(selectedEntityIds),
                TargetKind = TargetKind.Position,
                TargetEntityId = 0,
                TargetPos = QuantizedPos2.FromWorldXZ(worldPoint)
            };
        }

        public static SimCommand CreateAttackCommand(
            uint applyTick,
            byte playerId,
            uint commandSeq,
            List<uint> selectedEntityIds,
            uint targetEntityId,
            bool queued)
        {
            return new SimCommand
            {
                ApplyTick = applyTick,
                PlayerId = playerId,
                CommandSeq = commandSeq,
                Type = CommandType.Attack,
                Queued = queued,
                SelectedEntityIds = CanonicalizeEntityIds(selectedEntityIds),
                TargetKind = TargetKind.Entity,
                TargetEntityId = targetEntityId,
                TargetPos = default
            };
        }
    }
}
