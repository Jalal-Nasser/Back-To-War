using UnityEngine;

namespace CossacksRTS.Input
{
    /// <summary>
    /// Minimal scene setup:
    /// - Add this component to the main RTS camera object.
    /// - Ground is assumed to be XZ plane at Y = groundPlaneY.
    /// - Optional: clamp camera movement to map bounds.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float edgeScrollPixels = 12f;
        [SerializeField] private float edgeScrollSpeed = 25f;
        [SerializeField] private float middleMousePanSpeed = 1f;
        [SerializeField] private float groundPlaneY = 0f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 14f;
        [SerializeField] private float minCameraY = 12f;
        [SerializeField] private float maxCameraY = 75f;

        [Header("Bounds (Optional)")]
        [SerializeField] private bool clampToBounds = false;
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

        public void JumpToWorld(Vector2 worldXZ)
        {
            var p = transform.position;
            p.x = worldXZ.x;
            p.z = worldXZ.y;
            transform.position = p;
            ClampIfNeeded();
        }

        public void PanToWorld(Vector2 worldXZ, float lerpAlpha)
        {
            var p = transform.position;
            p.x = Mathf.Lerp(p.x, worldXZ.x, Mathf.Clamp01(lerpAlpha));
            p.z = Mathf.Lerp(p.z, worldXZ.y, Mathf.Clamp01(lerpAlpha));
            transform.position = p;
            ClampIfNeeded();
        }

        private void HandleEdgeScroll()
        {
            Vector3 delta = Vector3.zero;
            Vector3 mouse = Input.mousePosition;

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
            if (Input.GetMouseButtonDown(2))
            {
                _middlePanActive = TryGetGroundPoint(Input.mousePosition, out _middlePanStartWorld);
                _middlePanStartCameraPos = transform.position;
            }

            if (!_middlePanActive)
            {
                return;
            }

            if (Input.GetMouseButtonUp(2))
            {
                _middlePanActive = false;
                return;
            }

            if (TryGetGroundPoint(Input.mousePosition, out var currentWorld))
            {
                var delta = _middlePanStartWorld - currentWorld;
                transform.position = _middlePanStartCameraPos + delta * middleMousePanSpeed;
            }
        }

        private void HandleZoom()
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.0001f)
            {
                return;
            }

            var p = transform.position;
            p.y -= wheel * zoomSpeed;
            p.y = Mathf.Clamp(p.y, minCameraY, maxCameraY);
            transform.position = p;
        }

        private bool TryGetGroundPoint(Vector3 screenPos, out Vector3 world)
        {
            var ray = Camera.main != null ? Camera.main.ScreenPointToRay(screenPos) : default;
            if (ray.direction == Vector3.zero)
            {
                world = default;
                return false;
            }

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

            var p = transform.position;
            p.x = Mathf.Clamp(p.x, minBoundsXZ.x, maxBoundsXZ.x);
            p.z = Mathf.Clamp(p.z, minBoundsXZ.y, maxBoundsXZ.y);
            p.y = Mathf.Clamp(p.y, minCameraY, maxCameraY);
            transform.position = p;
        }
    }
}
