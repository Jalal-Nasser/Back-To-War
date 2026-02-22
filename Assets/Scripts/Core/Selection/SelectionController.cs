using System;
using System.Collections.Generic;
using UnityEngine;

namespace Back2War.Core.Selection
{
    /// <summary>
    /// Determinism constraints:
    /// - Selected entity IDs are always sorted ascending.
    /// - Shift toggles use deterministic binary-search insert/remove.
    /// - Hit tie-break is nearest distance key, then lower entity_id.
    /// </summary>
    public sealed class SelectionController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask selectableLayerMask = ~0;
        [SerializeField] private float selectableRaycastDistance = 10000f;
        [SerializeField] private bool debugLogs;

        private readonly List<uint> _selectedEntityIds = new List<uint>(256);
        private readonly RaycastHit[] _raycastHits = new RaycastHit[64];

        public IReadOnlyList<uint> SelectedEntityIdsSorted => _selectedEntityIds;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (selectableLayerMask == ~0)
            {
                int selectableLayer = LayerMask.NameToLayer("Selectable");
                if (selectableLayer >= 0)
                {
                    selectableLayerMask = 1 << selectableLayer;
                }
            }
        }

        public void HandleSelectionInput()
        {
            if (!global::UnityEngine.Input.GetMouseButtonDown(0))
            {
                return;
            }

            bool shiftHeld = IsShiftPressed();
            EntityId hitEntity = TryPickSelectableAtPointer();

            if (hitEntity == null)
            {
                if (!shiftHeld)
                {
                    ClearSelection();
                }

                return;
            }

            if (shiftHeld)
            {
                ToggleSelection(hitEntity.Id);
            }
            else
            {
                SelectSingle(hitEntity.Id);
            }

            if (debugLogs)
            {
                Debug.Log($"[SelectionController] Selected: {string.Join(",", _selectedEntityIds)}");
            }
        }

        public void ClearSelection()
        {
            _selectedEntityIds.Clear();
        }

        private void SelectSingle(uint entityId)
        {
            _selectedEntityIds.Clear();
            _selectedEntityIds.Add(entityId);
        }

        private void ToggleSelection(uint entityId)
        {
            int index = _selectedEntityIds.BinarySearch(entityId);
            if (index >= 0)
            {
                _selectedEntityIds.RemoveAt(index);
                return;
            }

            _selectedEntityIds.Insert(~index, entityId);
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
                _raycastHits,
                selectableRaycastDistance,
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
                Collider collider = _raycastHits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                EntityId entity = collider.GetComponentInParent<EntityId>();
                if (entity == null)
                {
                    continue;
                }

                long distanceKey = QuantizeDistance(_raycastHits[i].distance);
                if (distanceKey < bestDistanceKey || (distanceKey == bestDistanceKey && entity.Id < bestEntityId))
                {
                    bestEntity = entity;
                    bestDistanceKey = distanceKey;
                    bestEntityId = entity.Id;
                }
            }

            return bestEntity;
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
