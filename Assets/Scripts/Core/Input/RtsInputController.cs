using System.Text;
using Back2War.Core.Commands;
using Back2War.Core.Selection;
using Back2War.Core.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Back2War.Core.Input
{
    /// <summary>
    /// Routes mouse input into deterministic selection + command issuing.
    /// Command execution is handled later by CommandQueue/SimulationManager.
    /// </summary>
    public sealed class RtsInputController : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private SelectionController selection;
        [SerializeField] private LayerMask selectableMask;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private SimClock clock;
        [SerializeField] private CommandQueue queue;
        [SerializeField] private byte LocalPlayerId = 0;
        [SerializeField] private byte localPlayerTeamId = 0;
        [SerializeField] private bool debugLogs = true;

        private readonly StringBuilder _sb = new StringBuilder(128);
        private uint _commandSeq;

        private void Awake()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }

#pragma warning disable CS0618
            if (selection == null)
            {
                selection = FindObjectOfType<SelectionController>();
            }

            if (clock == null)
            {
                clock = FindObjectOfType<SimClock>();
            }

            if (queue == null)
            {
                queue = FindObjectOfType<CommandQueue>();
            }
#pragma warning restore CS0618

            if (selectableMask == 0)
            {
                int selectableLayer = LayerMask.NameToLayer("Selectable");
                if (selectableLayer >= 0)
                {
                    selectableMask = 1 << selectableLayer;
                }
            }

            if (groundMask == 0)
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer >= 0)
                {
                    groundMask = 1 << groundLayer;
                }
            }
        }

        private void Update()
        {
            if (selection == null || cam == null)
            {
                return;
            }

            // UI gate: no world interactions when pointer is over UI.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            HandleSelectionInput();
            HandleContextCommandInput();
        }

        private void HandleSelectionInput()
        {
            Vector2 mousePos = global::UnityEngine.Input.mousePosition;

            if (global::UnityEngine.Input.GetMouseButtonDown(0))
            {
                selection.BeginPointer(mousePos);
            }

            if (global::UnityEngine.Input.GetMouseButton(0))
            {
                selection.UpdatePointer(mousePos);
            }

            if (!global::UnityEngine.Input.GetMouseButtonUp(0))
            {
                return;
            }

            bool shiftHeld = IsShiftHeld();
            bool consumedAsDrag = selection.EndPointer(mousePos, shiftHeld, cam);
            if (consumedAsDrag)
            {
                return;
            }

            if (WorldRaycaster.RaycastSelectable(cam, mousePos, selectableMask, out var selectableHit))
            {
                uint hitId = selectableHit.Entity.Id;
                if (shiftHeld)
                {
                    selection.Toggle(hitId);
                }
                else
                {
                    selection.SelectSingle(hitId);
                }

                return;
            }

            if (!shiftHeld)
            {
                selection.Clear();
            }
        }

        private void HandleContextCommandInput()
        {
            if (!global::UnityEngine.Input.GetMouseButtonDown(1))
            {
                return;
            }

            if (clock == null || queue == null)
            {
                return;
            }

            var selected = selection.SelectedIdsSorted;
            if (selected == null || selected.Count == 0)
            {
                return;
            }

            bool queued = IsShiftHeld();
            Vector2 mousePos = global::UnityEngine.Input.mousePosition;

            if (WorldRaycaster.RaycastSelectable(cam, mousePos, selectableMask, out var selectableHit) &&
                selectableHit.Entity.TeamId != localPlayerTeamId)
            {
                SimCommand cmd = CommandFactory.CreateAttack(
                    selection,
                    clock,
                    LocalPlayerId,
                    ref _commandSeq,
                    queued,
                    selectableHit.Entity.Id);

                queue.Enqueue(cmd);

                if (debugLogs)
                {
                    Debug.Log("ISSUE ATTACK applyTick=" + cmd.ApplyTick +
                              " queued=" + (cmd.Queued ? "1" : "0") +
                              " sel=" + BuildIds(cmd.SelectedEntityIds) +
                              " target=" + cmd.TargetEntityId);
                }

                return;
            }

            if (WorldRaycaster.RaycastGround(cam, mousePos, groundMask, out Vector3 groundPoint))
            {
                SimCommand cmd = CommandFactory.CreateMove(
                    selection,
                    clock,
                    LocalPlayerId,
                    ref _commandSeq,
                    queued,
                    groundPoint);

                queue.Enqueue(cmd);

                if (debugLogs)
                {
                    Debug.Log("ISSUE MOVE applyTick=" + cmd.ApplyTick +
                              " queued=" + (cmd.Queued ? "1" : "0") +
                              " sel=" + BuildIds(cmd.SelectedEntityIds) +
                              " targetPos=(" + cmd.TargetPos.XMt + "," + cmd.TargetPos.YMt + ")");
                }
            }
        }

        private string BuildIds(System.Collections.Generic.IReadOnlyList<uint> ids)
        {
            _sb.Clear();
            _sb.Append('[');
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    _sb.Append(',');
                }

                _sb.Append(ids[i]);
            }

            _sb.Append(']');
            return _sb.ToString();
        }

        private static bool IsShiftHeld()
        {
            return global::UnityEngine.Input.GetKey(KeyCode.LeftShift) ||
                   global::UnityEngine.Input.GetKey(KeyCode.RightShift);
        }
    }
}
