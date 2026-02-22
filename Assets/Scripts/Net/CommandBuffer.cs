using System.Collections.Generic;
using UnityEngine;

namespace CossacksRTS.Net
{
    /// <summary>
    /// Deterministic per-tick command store.
    /// Commands are kept sorted by: (apply_tick, player_id, command_seq).
    /// </summary>
    public sealed class CommandBuffer : MonoBehaviour
    {
        [SerializeField] private bool debugLogs = true;

        private readonly SortedDictionary<int, List<DeterministicCommand>> _commandsByTick =
            new SortedDictionary<int, List<DeterministicCommand>>();

        public int TotalTicksBuffered => _commandsByTick.Count;

        public void Enqueue(DeterministicCommand command)
        {
            CanonicalizeSelectedIds(ref command);

            if (!_commandsByTick.TryGetValue(command.apply_tick, out var list))
            {
                list = new List<DeterministicCommand>(16);
                _commandsByTick.Add(command.apply_tick, list);
            }

            list.Add(command);
            list.Sort(CompareWithoutApplyTick);

            if (debugLogs)
            {
                Debug.Log($"[CommandBuffer] Enqueued {CommandSerializer.ToDebugString(command)}");
            }
        }

        public IReadOnlyList<DeterministicCommand> GetCommandsForTick(int applyTick)
        {
            if (_commandsByTick.TryGetValue(applyTick, out var list))
            {
                return list;
            }

            return System.Array.Empty<DeterministicCommand>();
        }

        public IReadOnlyList<DeterministicCommand> PopCommandsForTick(int applyTick)
        {
            if (_commandsByTick.TryGetValue(applyTick, out var list))
            {
                _commandsByTick.Remove(applyTick);
                return list;
            }

            return System.Array.Empty<DeterministicCommand>();
        }

        public void ClearAll()
        {
            _commandsByTick.Clear();
        }

        private static int CompareWithoutApplyTick(DeterministicCommand a, DeterministicCommand b)
        {
            int byPlayer = a.player_id.CompareTo(b.player_id);
            if (byPlayer != 0) return byPlayer;

            int bySeq = a.command_seq.CompareTo(b.command_seq);
            if (bySeq != 0) return bySeq;

            // Stable fallback in pathological ties.
            int byType = ((int)a.type).CompareTo((int)b.type);
            if (byType != 0) return byType;

            int aFirst = FirstSelected(a.selected_entity_ids);
            int bFirst = FirstSelected(b.selected_entity_ids);
            return aFirst.CompareTo(bFirst);
        }

        private static int FirstSelected(int[] ids)
        {
            return ids != null && ids.Length > 0 ? ids[0] : int.MaxValue;
        }

        private static void CanonicalizeSelectedIds(ref DeterministicCommand command)
        {
            if (command.selected_entity_ids == null || command.selected_entity_ids.Length <= 1)
            {
                return;
            }

            System.Array.Sort(command.selected_entity_ids);

            int uniqueCount = 1;
            for (int i = 1; i < command.selected_entity_ids.Length; i++)
            {
                if (command.selected_entity_ids[i] != command.selected_entity_ids[i - 1])
                {
                    command.selected_entity_ids[uniqueCount] = command.selected_entity_ids[i];
                    uniqueCount++;
                }
            }

            if (uniqueCount == command.selected_entity_ids.Length)
            {
                return;
            }

            var unique = new int[uniqueCount];
            System.Array.Copy(command.selected_entity_ids, unique, uniqueCount);
            command.selected_entity_ids = unique;
        }
    }
}
