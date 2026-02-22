using System;
using System.Text;
using UnityEngine;

namespace CossacksRTS.Net
{
    public enum CommandType : byte
    {
        Move = 1,
        Attack = 2,
        AttackMove = 3,
        Stop = 4,
        Hold = 5,
        Patrol = 6,
        Gather = 7,
        Build = 8,
        Train = 9,
        Research = 10,
        SetRallyPoint = 11,
        SetAlliance = 12
    }

    public enum CommandTargetKind : byte
    {
        None = 0,
        Entity = 1,
        Position = 2,
        Alliance = 3
    }

    public enum AllianceRelation : byte
    {
        Enemy = 0,
        Ally = 1,
        Neutral = 2
    }

    [Serializable]
    public struct QuantizedPosition2Int
    {
        public int x_mt;
        public int y_mt;

        public QuantizedPosition2Int(int xMt, int yMt)
        {
            x_mt = xMt;
            y_mt = yMt;
        }
    }

    [Serializable]
    public struct AllianceTarget
    {
        public int other_player_id;
        public AllianceRelation relation;
        public int shared_flags;
    }

    // Serialized payload for lockstep networking. No Vector3/Quaternion allowed.
    [Serializable]
    public struct DeterministicCommand
    {
        public int apply_tick;
        public int player_id;
        public int command_seq;
        public CommandType type;
        public bool queued;
        public int[] selected_entity_ids;       // Must be sorted asc, unique.
        public CommandTargetKind target_kind;
        public int target_entity_id;            // Valid when target_kind == Entity.
        public QuantizedPosition2Int target_pos;// Valid when target_kind == Position.
        public AllianceTarget alliance_target;  // Valid when target_kind == Alliance.
    }

    [Serializable]
    public struct CommandBatch
    {
        public string match_id;
        public int client_input_seq;
        public int ack_bundle_tick;
        public DeterministicCommand[] commands;
    }

    public static class CommandSerializer
    {
        public static string SerializeBatchToJson(CommandBatch batch, bool pretty = false)
        {
            return JsonUtility.ToJson(batch, pretty);
        }

        public static byte[] SerializeBatchToUtf8(CommandBatch batch, bool pretty = false)
        {
            return Encoding.UTF8.GetBytes(SerializeBatchToJson(batch, pretty));
        }

        public static CommandBatch DeserializeBatchFromJson(string json)
        {
            return JsonUtility.FromJson<CommandBatch>(json);
        }

        public static string SerializeCommandToJson(DeterministicCommand command, bool pretty = false)
        {
            return JsonUtility.ToJson(command, pretty);
        }

        public static string ToDebugString(DeterministicCommand command)
        {
            var selected = command.selected_entity_ids == null
                ? "[]"
                : $"[{string.Join(",", command.selected_entity_ids)}]";

            string target;
            switch (command.target_kind)
            {
                case CommandTargetKind.Entity:
                    target = $"entity:{command.target_entity_id}";
                    break;
                case CommandTargetKind.Position:
                    target = $"pos_mt:({command.target_pos.x_mt},{command.target_pos.y_mt})";
                    break;
                case CommandTargetKind.Alliance:
                    target = $"alliance:p{command.alliance_target.other_player_id}:{command.alliance_target.relation}";
                    break;
                default:
                    target = "none";
                    break;
            }

            return $"apply={command.apply_tick} p={command.player_id} seq={command.command_seq} " +
                   $"type={command.type} queued={command.queued} selected={selected} target={target}";
        }
    }
}
