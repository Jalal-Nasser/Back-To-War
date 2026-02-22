using System.Collections.Generic;
using UnityEngine;

namespace CossacksRTS.Input
{
    public enum SelectionContextHint : byte
    {
        None = 0,
        Resource = 1
    }

    /// <summary>
    /// Add to selectable world entities.
    /// Requires a collider for click raycasts.
    /// </summary>
    public sealed class SelectableEntity : MonoBehaviour
    {
        private static readonly List<SelectableEntity> Registry = new List<SelectableEntity>(2048);
        public static IReadOnlyList<SelectableEntity> All => Registry;

        [SerializeField] private int entityId = 1;
        [SerializeField] private int ownerPlayerId = 0;
        [SerializeField] private bool selectable = true;
        [SerializeField] private SelectionContextHint contextHint = SelectionContextHint.None;

        public int EntityId => entityId;
        public int OwnerPlayerId => ownerPlayerId;
        public bool IsSelectable => selectable && isActiveAndEnabled;
        public SelectionContextHint ContextHint => contextHint;

        private void OnEnable()
        {
            if (!Registry.Contains(this))
            {
                Registry.Add(this);
            }
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }
    }

    /// <summary>
    /// Minimal scene setup:
    /// - Create GameObject "SelectionManager" and add this component.
    /// - Assign World Camera.
    /// - Add SelectableEntity + Collider to unit prefabs.
    /// </summary>
    public sealed class SelectionManager : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask selectableRaycastMask = ~0;
        [SerializeField] private float dragThresholdPixels = 8f;
        [SerializeField] private bool debugLogs = true;

        private readonly List<SelectableEntity> _selection = new List<SelectableEntity>(512);

        private bool _pointerSelectionActive;
        private bool _boxSelecting;
        private Vector2 _dragStart;
        private Vector2 _dragCurrent;

        private static Texture2D s_dragTexture;

        public bool HasSelection => _selection.Count > 0;
        public bool IsBoxSelecting => _boxSelecting;
        public IReadOnlyList<SelectableEntity> SelectedEntities => _selection;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (s_dragTexture == null)
            {
                s_dragTexture = new Texture2D(1, 1);
                s_dragTexture.SetPixel(0, 0, new Color(0.2f, 0.8f, 0.2f, 0.2f));
                s_dragTexture.Apply();
            }
        }

        public void BeginPointerSelection(Vector2 screenPos)
        {
            _pointerSelectionActive = true;
            _boxSelecting = false;
            _dragStart = screenPos;
            _dragCurrent = screenPos;
        }

        public void UpdatePointerSelection(Vector2 screenPos)
        {
            if (!_pointerSelectionActive)
            {
                return;
            }

            _dragCurrent = screenPos;
            if (!_boxSelecting && (_dragCurrent - _dragStart).sqrMagnitude >= dragThresholdPixels * dragThresholdPixels)
            {
                _boxSelecting = true;
            }
        }

        public void EndPointerSelection(Vector2 screenPos, bool shiftToggle)
        {
            if (!_pointerSelectionActive)
            {
                return;
            }

            _dragCurrent = screenPos;

            if (_boxSelecting)
            {
                var candidates = CollectBoxSelection(_dragStart, _dragCurrent);
                ApplySelection(candidates, shiftToggle);
            }
            else
            {
                var hit = HitSelectableAtScreenPoint(_dragCurrent);
                if (hit == null)
                {
                    if (!shiftToggle)
                    {
                        ClearSelection();
                    }
                }
                else
                {
                    ApplySelection(new List<SelectableEntity> { hit }, shiftToggle);
                }
            }

            _pointerSelectionActive = false;
            _boxSelecting = false;
        }

        public int[] GetSelectionIdsDeterministic()
        {
            var ids = new int[_selection.Count];
            for (int i = 0; i < _selection.Count; i++)
            {
                ids[i] = _selection[i].EntityId;
            }

            return ids;
        }

        public bool IsEntitySelected(int entityId)
        {
            for (int i = 0; i < _selection.Count; i++)
            {
                if (_selection[i].EntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        public void ClearSelection()
        {
            _selection.Clear();
        }

        private List<SelectableEntity> CollectBoxSelection(Vector2 start, Vector2 end)
        {
            var rect = BuildScreenRect(start, end);
            var hits = new List<SelectableEntity>(128);
            var all = SelectableEntity.All;

            for (int i = 0; i < all.Count; i++)
            {
                var selectable = all[i];
                if (selectable == null || !selectable.IsSelectable)
                {
                    continue;
                }

                var screen = worldCamera.WorldToScreenPoint(selectable.transform.position);
                if (screen.z < 0f)
                {
                    continue;
                }

                if (rect.Contains(new Vector2(screen.x, screen.y), true))
                {
                    hits.Add(selectable);
                }
            }

            hits.Sort((a, b) => a.EntityId.CompareTo(b.EntityId));
            return hits;
        }

        private SelectableEntity HitSelectableAtScreenPoint(Vector2 screenPos)
        {
            var ray = worldCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 10000f, selectableRaycastMask, QueryTriggerInteraction.Ignore))
            {
                var selectable = hit.collider.GetComponentInParent<SelectableEntity>();
                if (selectable != null && selectable.IsSelectable)
                {
                    return selectable;
                }
            }

            return null;
        }

        private void ApplySelection(List<SelectableEntity> candidates, bool shiftToggle)
        {
            if (!shiftToggle)
            {
                _selection.Clear();
                _selection.AddRange(candidates);
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    int existing = FindSelectionIndex(candidate.EntityId);
                    if (existing >= 0)
                    {
                        _selection.RemoveAt(existing);
                    }
                    else
                    {
                        _selection.Add(candidate);
                    }
                }
            }

            _selection.Sort((a, b) => a.EntityId.CompareTo(b.EntityId));

            if (debugLogs)
            {
                Debug.Log($"[Selection] [{string.Join(",", GetSelectionIdsDeterministic())}]");
            }
        }

        private int FindSelectionIndex(int entityId)
        {
            for (int i = 0; i < _selection.Count; i++)
            {
                if (_selection[i].EntityId == entityId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static Rect BuildScreenRect(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float xMax = Mathf.Max(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float yMax = Mathf.Max(a.y, b.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void OnGUI()
        {
            if (!_boxSelecting)
            {
                return;
            }

            var rect = BuildScreenRect(_dragStart, _dragCurrent);
            // Convert from bottom-left (screen) to top-left (OnGUI) coordinates.
            rect.y = Screen.height - rect.yMax;
            GUI.DrawTexture(rect, s_dragTexture);
            GUI.Box(rect, GUIContent.none);
        }
    }
}
