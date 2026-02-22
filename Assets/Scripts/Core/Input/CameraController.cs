using UnityEngine;

namespace Back2War.Core.Input
{
    /// <summary>
    /// Determinism note:
    /// - Camera movement is local presentation only and does not affect command payload determinism.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private float edgeScrollPixels = 12f;
        [SerializeField] private float edgeScrollSpeed = 25f;
        [SerializeField] private float middleMousePanSpeed = 1f;
        [SerializeField] private float groundPlaneY = 0f;

        [SerializeField] private float zoomSpeed = 14f;
        [SerializeField] private float minCameraY = 12f;
        [SerializeField] private float maxCameraY = 75f;

        [SerializeField] private bool clampToBounds;
        [SerializeField] private Vector2 minBoundsXZ = new Vector2(-128f, -128f);
        [SerializeField] private Vector2 maxBoundsXZ = new Vector2(128f, 128f);

        private bool _middlePanActive;
        private Vector3 _middlePanStartWorld;
        private Vector3 _middlePanStartCameraPos;

        private void Update()
        {
            HandleEdgeScroll();
            HandleMiddleMousePan();
            HandleZoom();
            ClampIfNeeded();
        }

        private void HandleEdgeScroll()
        {
            Vector3 delta = Vector3.zero;
            Vector3 mouse = global::UnityEngine.Input.mousePosition;

            if (mouse.x <= edgeScrollPixels) delta.x -= 1f;
            else if (mouse.x >= Screen.width - edgeScrollPixels) delta.x += 1f;

            if (mouse.y <= edgeScrollPixels) delta.z -= 1f;
            else if (mouse.y >= Screen.height - edgeScrollPixels) delta.z += 1f;

            if (delta.sqrMagnitude > 0f)
            {
                delta.Normalize();
                transform.position += delta * (edgeScrollSpeed * Time.unscaledDeltaTime);
            }
        }

        private void HandleMiddleMousePan()
        {
            if (global::UnityEngine.Input.GetMouseButtonDown(2))
            {
                _middlePanActive = TryGetGroundPoint(global::UnityEngine.Input.mousePosition, out _middlePanStartWorld);
                _middlePanStartCameraPos = transform.position;
            }

            if (!_middlePanActive)
            {
                return;
            }

            if (global::UnityEngine.Input.GetMouseButtonUp(2))
            {
                _middlePanActive = false;
                return;
            }

            if (TryGetGroundPoint(global::UnityEngine.Input.mousePosition, out Vector3 currentWorld))
            {
                Vector3 delta = _middlePanStartWorld - currentWorld;
                transform.position = _middlePanStartCameraPos + delta * middleMousePanSpeed;
            }
        }

        private void HandleZoom()
        {
            float wheel = global::UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.0001f)
            {
                return;
            }

            Vector3 position = transform.position;
            position.y -= wheel * zoomSpeed;
            position.y = Mathf.Clamp(position.y, minCameraY, maxCameraY);
            transform.position = position;
        }

        private bool TryGetGroundPoint(Vector3 screenPos, out Vector3 world)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                world = default;
                return false;
            }

            Ray ray = cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }

            world = default;
            return false;
        }

        private void ClampIfNeeded()
        {
            if (!clampToBounds)
            {
                return;
            }

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, minBoundsXZ.x, maxBoundsXZ.x);
            position.z = Mathf.Clamp(position.z, minBoundsXZ.y, maxBoundsXZ.y);
            position.y = Mathf.Clamp(position.y, minCameraY, maxCameraY);
            transform.position = position;
        }
    }
}
