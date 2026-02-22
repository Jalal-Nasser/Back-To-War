using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CossacksRTS.UI
{
    /// <summary>
    /// Blocks world input when pointer is over UI.
    /// Attach to a scene singleton object (e.g. "UIBlocker").
    /// </summary>
    public sealed class UIBlocker : MonoBehaviour
    {
        [SerializeField] private bool blockWhenPointerOverUi = true;
        [SerializeField] private List<GraphicRaycaster> blockingRaycasters = new List<GraphicRaycaster>();

        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);
        private PointerEventData _pointerEventData;

        public bool IsPointerBlocked()
        {
            if (!blockWhenPointerOverUi)
            {
                return false;
            }

            if (EventSystem.current == null)
            {
                return false;
            }

            if (EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

            if (blockingRaycasters == null || blockingRaycasters.Count == 0)
            {
                return false;
            }

            if (_pointerEventData == null)
            {
                _pointerEventData = new PointerEventData(EventSystem.current);
            }

            _pointerEventData.position = Input.mousePosition;
            _raycastResults.Clear();

            for (int i = 0; i < blockingRaycasters.Count; i++)
            {
                var raycaster = blockingRaycasters[i];
                if (raycaster == null || !raycaster.isActiveAndEnabled)
                {
                    continue;
                }

                raycaster.Raycast(_pointerEventData, _raycastResults);
                if (_raycastResults.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
