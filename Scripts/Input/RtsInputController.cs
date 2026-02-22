using CossacksRTS.Net;
using CossacksRTS.UI;
using UnityEngine;

namespace CossacksRTS.Input
{
    /// <summary>
    /// Minimal scene setup:
    /// 1) Create GameObject "GameSystems" and add:
    ///    - CommandBuffer
    ///    - UIBlocker
    /// 2) Add SelectionManager on "SelectionManager" object and assign World Camera.
    /// 3) Add this RtsInputController to "GameSystems" and wire references:
    ///    - SelectionManager
    ///    - CommandBuffer
    ///    - UIBlocker
    ///    - World Camera
    /// 4) Add SelectableEntity + Collider to unit prefabs.
    /// 5) (Optional debug) Add DebugHud to "GameSystems" and wire:
    ///    - RtsInputController
    ///    - SelectionManager
    ///    - CommandBuffer
    ///
    /// This controller only produces deterministic command payloads.
    /// It does NOT run gameplay simulation.
    /// </summary>
    public sealed class RtsInputController : MonoBehaviour
    {
        private enum ArmedOrder
        {
            None = 0,
            AttackMove = 1,
            Patrol = 2,
            SetRally = 3
        }

        [Header("References")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SelectionManager selectionManager;
        [SerializeField] private CommandBuffer commandBuffer;
        [SerializeField] private UIBlocker uiBlocker;

        [Header("Raycast")]
        [SerializeField] private LayerMask commandRaycastMask = ~0;
        [SerializeField] private float commandRaycastDistance = 10000f;

        [Header("Lockstep Tick")]
        [SerializeField] private int playerId = 0;
        [SerializeField] private int inputDelayTicks = 3;
        [SerializeField] private int localTickRateHz = 20;
        [SerializeField] private bool autoAdvanceLocalTick = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private int _localTick;
        private int _nextCommandSeq;
        private float _tickAccumulator;
        private bool _selectionBlockedByUi;
        private ArmedOrder _armedOrder = ArmedOrder.None;
        private bool _hasLastProducedCommand;
        private DeterministicCommand _lastProducedCommand;

        public int LocalTick => _localTick;
        public bool HasLastProducedCommand => _hasLastProducedCommand;
        public DeterministicCommand LastProducedCommand => _lastProducedCommand;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (selectionManager == null) selectionManager = FindObjectOfType<SelectionManager>();
            if (commandBuffer == null) commandBuffer = FindObjectOfType<CommandBuffer>();
            if (uiBlocker == null) uiBlocker = FindObjectOfType<UIBlocker>();
        }

        private void Update()
        {
            AdvanceLocalTick();
            HandleSelectionInput();
            HandleCommandModeHotkeys();
            HandleImmediateHotkeys();
            HandleContextRightClick();
        }

        public void SetLocalTick(int authoritativeTick)
        {
            _localTick = Mathf.Max(0, authoritativeTick);
        }

        public bool TryGetLastProducedCommand(out DeterministicCommand command)
        {
            command = _lastProducedCommand;
            return _hasLastProducedCommand;
        }

        private void AdvanceLocalTick()
        {
            if (!autoAdvanceLocalTick)
            {
                return;
            }

            float step = 1f / Mathf.Max(1, localTickRateHz);
            _tickAccumulator += Time.unscaledDeltaTime;

            while (_tickAccumulator >= step)
            {
                _tickAccumulator -= step;
                _localTick++;
            }
        }

        private void HandleSelectionInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _selectionBlockedByUi = IsUiBlocked();
                if (!_selectionBlockedByUi)
                {
                    selectionManager.BeginPointerSelection(Input.mousePosition);
                }
            }

            if (Input.GetMouseButton(0) && !_selectionBlockedByUi)
            {
                selectionManager.UpdatePointerSelection(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (!_selectionBlockedByUi)
                {
                    bool shift = IsShiftPressed();
                    selectionManager.EndPointerSelection(Input.mousePosition, shift);
                }

                _selectionBlockedByUi = false;
            }
        }

        private void HandleCommandModeHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                _armedOrder = ArmedOrder.AttackMove;
                if (debugLogs) Debug.Log("[RtsInput] Armed order: AttackMove");
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                _armedOrder = ArmedOrder.Patrol;
                if (debugLogs) Debug.Log("[RtsInput] Armed order: Patrol");
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _armedOrder = ArmedOrder.SetRally;
                if (debugLogs) Debug.Log("[RtsInput] Armed order: SetRallyPoint");
            }
        }

        private void HandleImmediateHotkeys()
        {
            if (!selectionManager.HasSelection)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                EnqueueSelectionCommand(CommandType.Stop, CommandTargetKind.None, 0, default, IsShiftPressed());
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                EnqueueSelectionCommand(CommandType.Hold, CommandTargetKind.None, 0, default, IsShiftPressed());
            }
        }

        private void HandleContextRightClick()
        {
            if (!Input.GetMouseButtonDown(1))
            {
                return;
            }

            if (IsUiBlocked())
            {
                return;
            }

            if (!selectionManager.HasSelection)
            {
                return;
            }

            if (!TryRaycastFromMouse(out var hit))
            {
                return;
            }

            bool queued = IsShiftPressed();
            var targetPos = Quantization.FromWorldPoint(hit.point);

            CommandType commandType;
            CommandTargetKind targetKind;
            int targetEntityId = 0;

            var hitSelectable = hit.collider.GetComponentInParent<SelectableEntity>();
            if (_armedOrder == ArmedOrder.SetRally)
            {
                commandType = CommandType.SetRallyPoint;
                targetKind = CommandTargetKind.Position;
                _armedOrder = ArmedOrder.None;
            }
            else if (_armedOrder == ArmedOrder.Patrol)
            {
                commandType = CommandType.Patrol;
                targetKind = CommandTargetKind.Position;
                _armedOrder = ArmedOrder.None;
            }
            else if (_armedOrder == ArmedOrder.AttackMove)
            {
                commandType = CommandType.AttackMove;
                targetKind = CommandTargetKind.Position;
                _armedOrder = ArmedOrder.None;
            }
            else if (hitSelectable != null && hitSelectable.IsSelectable && hitSelectable.OwnerPlayerId != playerId)
            {
                commandType = CommandType.Attack;
                targetKind = CommandTargetKind.Entity;
                targetEntityId = hitSelectable.EntityId;
            }
            else if (hitSelectable != null && hitSelectable.ContextHint == SelectionContextHint.Resource)
            {
                commandType = CommandType.Gather;
                targetKind = CommandTargetKind.Entity;
                targetEntityId = hitSelectable.EntityId;
            }
            else
            {
                commandType = CommandType.Move;
                targetKind = CommandTargetKind.Position;
            }

            EnqueueSelectionCommand(commandType, targetKind, targetEntityId, targetPos, queued);
        }

        private void EnqueueSelectionCommand(
            CommandType commandType,
            CommandTargetKind targetKind,
            int targetEntityId,
            QuantizedPosition2Int targetPos,
            bool queued)
        {
            var selected = selectionManager.GetSelectionIdsDeterministic();
            if (selected == null || selected.Length == 0)
            {
                return;
            }

            var command = new DeterministicCommand
            {
                apply_tick = _localTick + Mathf.Max(0, inputDelayTicks),
                player_id = playerId,
                command_seq = ++_nextCommandSeq,
                type = commandType,
                queued = queued,
                selected_entity_ids = selected,
                target_kind = targetKind,
                target_entity_id = targetEntityId,
                target_pos = targetPos,
                alliance_target = default
            };

            commandBuffer.Enqueue(command);
            _lastProducedCommand = command;
            _hasLastProducedCommand = true;

            if (debugLogs)
            {
                Debug.Log($"[RtsInput] {CommandSerializer.ToDebugString(command)}");
            }
        }

        private bool TryRaycastFromMouse(out RaycastHit hit)
        {
            var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out hit, commandRaycastDistance, commandRaycastMask, QueryTriggerInteraction.Ignore);
        }

        private bool IsUiBlocked()
        {
            return uiBlocker != null && uiBlocker.IsPointerBlocked();
        }

        private static bool IsShiftPressed()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}
