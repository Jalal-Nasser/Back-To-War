using System;
using System.Collections.Generic;
using Back2War.Core.Commands;
using Back2War.Core.Selection;
using Back2War.Core.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Back2War.Core.Input
{
    /// <summary>
    /// Determinism constraints:
    /// - Generates commands with apply_tick = sim_tick + input_delay_ticks.
    /// - Multi-entity selections are canonicalized before command enqueue.
    /// - Target tie-break for entity hits: nearest distance key, then lower entity_id.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SelectionController selectionController;
        [SerializeField] private SimClock simClock;
        [SerializeField] private CommandQueue commandQueue;

        [SerializeField] private byte localPlayerId = 0;
        [SerializeField] private LayerMask selectableLayerMask = ~0;
        [SerializeField] private float entityRaycastDistance = 10000f;
        [SerializeField] private bool debugLogs = true;

        private readonly RaycastHit[] _entityHits = new RaycastHit[64];
        private uint _nextCommandSeq;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

#pragma warning disable CS0618
            if (selectionController == null)
            {
                selectionController = FindObjectOfType<SelectionController>();
            }

            if (simClock == null)
            {
                simClock = FindObjectOfType<SimClock>();
            }

            if (commandQueue == null)
            {
                commandQueue = FindObjectOfType<CommandQueue>();
            }
#pragma warning restore CS0618

            if (selectableLayerMask == ~0)
            {
                int selectableLayer = LayerMask.NameToLayer("Selectable");
                if (selectableLayer >= 0)
                {
                    selectableLayerMask = 1 << selectableLayer;
                }
            }
        }

        private void Update()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (selectionController != null)
            {
                selectionController.HandleSelectionInput();
            }

            HandleRightClickCommand();
        }

        private void HandleRightClickCommand()
        {
            if (!global::UnityEngine.Input.GetMouseButtonDown(1))
            {
                return;
            }

            if (selectionController == null || simClock == null || commandQueue == null)
            {
                return;
            }

            IReadOnlyList<uint> selection = selectionController.SelectedEntityIdsSorted;
            if (selection == null || selection.Count == 0)
            {
                return;
            }

            bool queued = IsShiftPressed();
            uint applyTick = simClock.ComputeApplyTick();
            uint commandSeq = ++_nextCommandSeq;

            List<uint> selectedEntityIds = CopySelection(selection);
            SimCommand command;

            EntityId hitEntity = TryPickSelectableAtPointer();
            if (hitEntity != null && hitEntity.OwnerPlayerId != localPlayerId)
            {
                command = CommandSerializer.CreateAttackCommand(
                    applyTick,
                    localPlayerId,
                    commandSeq,
                    selectedEntityIds,
                    hitEntity.Id,
                    queued);
            }
            else
            {
                if (!TryGetGroundPoint(global::UnityEngine.Input.mousePosition, out Vector3 worldPoint))
                {
                    return;
                }

                command = CommandSerializer.CreateMoveCommand(
                    applyTick,
                    localPlayerId,
                    commandSeq,
                    selectedEntityIds,
                    worldPoint,
                    queued);
            }

            commandQueue.Enqueue(command);

            if (debugLogs)
            {
                Debug.Log(
                    $"[InputController] Enqueued {command.Type} apply={command.ApplyTick} player={command.PlayerId} seq={command.CommandSeq} " +
                    $"targetEntity={command.TargetEntityId} targetPos=({command.TargetPos.XMt},{command.TargetPos.YMt}) selected={command.SelectedEntityIds.Length}");
            }
        }

        private EntityId TryPickSelectableAtPointer()
        {
            if (worldCamera == null)
            {
                return null;
            }

            Ray ray = worldCamera.ScreenPointToRay(global::UnityEngine.Input.mousePosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _entityHits,
                entityRaycastDistance,
                selectableLayerMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return null;
            }

            EntityId bestEntity = null;
            long bestDistanceKey = long.MaxValue;
            uint bestEntityId = uint.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = _entityHits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                EntityId entity = collider.GetComponentInParent<EntityId>();
                if (entity == null)
                {
                    continue;
                }

                long distanceKey = QuantizeDistance(_entityHits[i].distance);
                if (distanceKey < bestDistanceKey || (distanceKey == bestDistanceKey && entity.Id < bestEntityId))
                {
                    bestEntity = entity;
                    bestDistanceKey = distanceKey;
                    bestEntityId = entity.Id;
                }
            }

            return bestEntity;
        }

        private bool TryGetGroundPoint(Vector3 mousePosition, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (worldCamera == null)
            {
                return false;
            }

            var plane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = worldCamera.ScreenPointToRay(mousePosition);
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            worldPoint = ray.GetPoint(distance);
            return true;
        }

        private static List<uint> CopySelection(IReadOnlyList<uint> selectedIds)
        {
            var list = new List<uint>(selectedIds.Count);
            for (int i = 0; i < selectedIds.Count; i++)
            {
                list.Add(selectedIds[i]);
            }

            return list;
        }

        private static long QuantizeDistance(float value)
        {
            return (long)Math.Round(value * 1000000.0d);
        }

        private static bool IsShiftPressed()
        {
            return global::UnityEngine.Input.GetKey(KeyCode.LeftShift) ||
                   global::UnityEngine.Input.GetKey(KeyCode.RightShift);
        }
    }
}
