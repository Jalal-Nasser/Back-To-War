using System.Text;
using CossacksRTS.Input;
using CossacksRTS.Net;
using UnityEngine;

namespace CossacksRTS.Debugging
{
    /// <summary>
    /// Lightweight runtime HUD for input/command visibility.
    ///
    /// Minimal scene setup:
    /// - Add this component to "GameSystems" (or another always-active object).
    /// - Assign RtsInputController, SelectionManager, and CommandBuffer references.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RtsInputController inputController;
        [SerializeField] private SelectionManager selectionManager;
        [SerializeField] private CommandBuffer commandBuffer;

        [Header("Display")]
        [SerializeField] private bool showHud = true;
        [SerializeField] private int nextTicksWindow = 8;
        [SerializeField] private Vector2 panelPosition = new Vector2(12f, 12f);
        [SerializeField] private Vector2 panelSize = new Vector2(560f, 220f);

        private readonly StringBuilder _builder = new StringBuilder(1024);
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;

        private void Awake()
        {
            if (inputController == null) inputController = FindObjectOfType<RtsInputController>();
            if (selectionManager == null) selectionManager = FindObjectOfType<SelectionManager>();
            if (commandBuffer == null) commandBuffer = FindObjectOfType<CommandBuffer>();
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            EnsureStyles();

            var rect = new Rect(panelPosition.x, panelPosition.y, panelSize.x, panelSize.y);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            _builder.Length = 0;
            AppendTickInfo();
            AppendSelectionInfo();
            AppendLastCommandInfo();
            AppendBufferWindowInfo();

            var labelRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
            GUI.Label(labelRect, _builder.ToString(), _labelStyle);
        }

        private void AppendTickInfo()
        {
            int localTick = inputController != null ? inputController.LocalTick : -1;
            _builder.Append("local_tick: ").Append(localTick).Append('\n');
        }

        private void AppendSelectionInfo()
        {
            if (selectionManager == null)
            {
                _builder.Append("selection: <missing SelectionManager>\n");
                return;
            }

            var selected = selectionManager.SelectedEntities;
            int count = selected.Count;

            _builder.Append("selection_count: ").Append(count).Append("  first5_ids: [");
            int limit = Mathf.Min(5, count);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) _builder.Append(',');
                _builder.Append(selected[i].EntityId);
            }
            _builder.Append(']').Append('\n');
        }

        private void AppendLastCommandInfo()
        {
            if (inputController == null)
            {
                _builder.Append("last_command: <missing RtsInputController>\n");
                return;
            }

            if (!inputController.TryGetLastProducedCommand(out var command))
            {
                _builder.Append("last_command: <none>\n");
                return;
            }

            _builder.Append("last_command: type=").Append(command.type)
                .Append(" apply_tick=").Append(command.apply_tick)
                .Append(" queued=").Append(command.queued ? "1" : "0").Append(' ');

            switch (command.target_kind)
            {
                case CommandTargetKind.Entity:
                    _builder.Append("target_entity=").Append(command.target_entity_id);
                    break;
                case CommandTargetKind.Position:
                    _builder.Append("target_pos_mt=(")
                        .Append(command.target_pos.x_mt)
                        .Append(',')
                        .Append(command.target_pos.y_mt)
                        .Append(')');
                    break;
                case CommandTargetKind.Alliance:
                    _builder.Append("target_alliance=p")
                        .Append(command.alliance_target.other_player_id)
                        .Append(':')
                        .Append(command.alliance_target.relation);
                    break;
                default:
                    _builder.Append("target=<none>");
                    break;
            }

            _builder.Append('\n');
        }

        private void AppendBufferWindowInfo()
        {
            if (commandBuffer == null || inputController == null)
            {
                _builder.Append("buffer_next_ticks: <missing refs>\n");
                return;
            }

            int localTick = inputController.LocalTick;
            int window = Mathf.Max(1, nextTicksWindow);
            int total = 0;

            _builder.Append("buffer_next_").Append(window).Append("_ticks: ");
            for (int i = 0; i < window; i++)
            {
                int tick = localTick + i;
                int count = commandBuffer.GetCommandsForTick(tick).Count;
                total += count;

                if (i > 0) _builder.Append(" | ");
                _builder.Append(tick).Append(':').Append(count);
            }

            _builder.Append("  total=").Append(total).Append('\n');
        }

        private void EnsureStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13,
                    richText = false
                };
            }

            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal =
                    {
                        textColor = Color.white
                    }
                };
            }
        }
    }
}
