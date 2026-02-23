using System.Collections.Generic;
using Back2War.Core.World;
using UnityEngine;

namespace Back2War.Core.Selection
{
    /// <summary>
    /// Deterministic selection store and drag-box selection.
    /// selectedIds stays sorted ascending and unique at all times.
    /// </summary>
    public sealed class SelectionController : MonoBehaviour
    {
        [SerializeField] private float dragThresholdPixels = 8f;

        private readonly List<uint> _selectedIds = new List<uint>(256);

        private bool _pointerHeld;
        private bool _dragActive;
        private Vector2 _dragStart;
        private Vector2 _dragCurrent;

        private static Texture2D s_BoxTexture;

        public IReadOnlyList<uint> SelectedIdsSorted => _selectedIds;
        public IReadOnlyList<uint> SelectedEntityIdsSorted => _selectedIds; // backward-compat
        public bool IsDragging => _dragActive;

        private void Awake()
        {
            if (s_BoxTexture == null)
            {
                s_BoxTexture = new Texture2D(1, 1);
                s_BoxTexture.SetPixel(0, 0, new Color(0.2f, 0.9f, 0.3f, 0.2f));
                s_BoxTexture.Apply();
            }
        }

        public void BeginPointer(Vector2 screenPos)
        {
            _pointerHeld = true;
            _dragActive = false;
            _dragStart = screenPos;
            _dragCurrent = screenPos;
        }

        public void UpdatePointer(Vector2 screenPos)
        {
            if (!_pointerHeld)
            {
                return;
            }

            _dragCurrent = screenPos;
            if (!_dragActive)
            {
                float sqr = (_dragCurrent - _dragStart).sqrMagnitude;
                if (sqr >= dragThresholdPixels * dragThresholdPixels)
                {
                    _dragActive = true;
                }
            }
        }

        public bool EndPointer(Vector2 screenPos, bool shiftHeld, Camera cam)
        {
            if (!_pointerHeld)
            {
                return false;
            }

            _dragCurrent = screenPos;
            bool consumedAsDrag = _dragActive;
            _pointerHeld = false;

            if (consumedAsDrag)
            {
                ApplyDragBoxSelection(shiftHeld, cam);
            }

            _dragActive = false;
            return consumedAsDrag;
        }

        public void SelectSingle(uint entityId)
        {
            _selectedIds.Clear();
            _selectedIds.Add(entityId);
        }

        public void Toggle(uint entityId)
        {
            int idx = _selectedIds.BinarySearch(entityId);
            if (idx >= 0)
            {
                _selectedIds.RemoveAt(idx);
            }
            else
            {
                _selectedIds.Insert(~idx, entityId);
            }
        }

        public void Clear()
        {
            _selectedIds.Clear();
        }

        // Legacy entry-point retained for older scripts still in the project.
        public void HandleSelectionInput()
        {
        }

        private void ApplyDragBoxSelection(bool shiftHeld, Camera cam)
        {
            if (cam == null)
            {
                return;
            }

            Rect box = BuildRect(_dragStart, _dragCurrent);
            Selectable[] selectables = Object.FindObjectsByType<Selectable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var boxIds = new List<uint>(selectables.Length);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null || !selectable.isActiveAndEnabled)
                {
                    continue;
                }

                var entity = selectable.GetComponent<EntityId>();
                if (entity == null)
                {
                    continue;
                }

                Vector3 screen = cam.WorldToScreenPoint(selectable.transform.position);
                if (screen.z <= 0f)
                {
                    continue;
                }

                if (box.Contains(new Vector2(screen.x, screen.y), true))
                {
                    boxIds.Add(entity.Id);
                }
            }

            if (boxIds.Count == 0)
            {
                if (!shiftHeld)
                {
                    _selectedIds.Clear();
                }

                return;
            }

            boxIds.Sort();

            int write = 1;
            for (int read = 1; read < boxIds.Count; read++)
            {
                if (boxIds[read] != boxIds[read - 1])
                {
                    boxIds[write] = boxIds[read];
                    write++;
                }
            }

            if (write < boxIds.Count)
            {
                boxIds.RemoveRange(write, boxIds.Count - write);
            }

            if (!shiftHeld)
            {
                _selectedIds.Clear();
                _selectedIds.AddRange(boxIds);
                return;
            }

            for (int i = 0; i < boxIds.Count; i++)
            {
                Toggle(boxIds[i]);
            }
        }

        private void OnGUI()
        {
            if (!_dragActive)
            {
                return;
            }

            Rect screenRect = BuildRect(_dragStart, _dragCurrent);
            // Screen-space (bottom-left) -> GUI-space (top-left)
            screenRect.y = Screen.height - screenRect.yMax;

            GUI.DrawTexture(screenRect, s_BoxTexture);
            GUI.Box(screenRect, GUIContent.none);
        }

        private static Rect BuildRect(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float xMax = Mathf.Max(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float yMax = Mathf.Max(a.y, b.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
