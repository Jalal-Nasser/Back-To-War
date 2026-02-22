using System;
using System.Collections.Generic;
using Back2War.Core.Commands;
using UnityEngine;

namespace Back2War.Core.Input
{
    /// <summary>
    /// Determinism constraints:
    /// - Quantization snaps world hits to tile centers (milli-tiles).
    /// - Entity lists are canonicalized (sorted ascending, unique).
    /// - No raw floating values are serialized in SimCommand payloads.
    /// </summary>
    public static class CommandSerializer
    {
        private const int MilliTilesPerTile = 1000;
        private const int TileCenterOffsetMt = 500;

        public static QuantizedPos2 WorldToTileCenterQuantized(Vector3 worldPoint)
        {
            int tileX = (int)Math.Floor(worldPoint.x);
            int tileY = (int)Math.Floor(worldPoint.z);

            int xMt = checked(tileX * MilliTilesPerTile + TileCenterOffsetMt);
            int yMt = checked(tileY * MilliTilesPerTile + TileCenterOffsetMt);
            return new QuantizedPos2(xMt, yMt);
        }

        public static uint[] CanonicalizeEntityIds(List<uint> entityIds)
        {
            if (entityIds == null || entityIds.Count == 0)
            {
                return Array.Empty<uint>();
            }

            var sorted = entityIds.ToArray();
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

            var unique = new uint[write];
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
                TargetPos = WorldToTileCenterQuantized(worldPoint),
                Alliance = default
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
                TargetPos = default,
                Alliance = default
            };
        }
    }
}
