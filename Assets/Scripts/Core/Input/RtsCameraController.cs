using UnityEngine;

namespace Back2War.Core.Input
{
    /// <summary>
    /// Deterministic input note:
    /// - Camera control is client-side presentation only and is not part of lockstep simulation.
    /// - No gameplay state is modified here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class RtsCameraController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float panSpeed = 20f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float minOrthographicSize = 8f;
        [SerializeField] private float maxOrthographicSize = 40f;

        [Header("Edge Scroll (Optional)")]
        [SerializeField] private bool edgeScrollEnabled = false;
        [SerializeField] private float edgeScrollPixels = 8f;
        [SerializeField] private float edgeScrollSpeed = 20f;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, minOrthographicSize, maxOrthographicSize);
        }

        private void Update()
        {
            HandlePan();
            HandleZoom();

            if (edgeScrollEnabled)
            {
                // TODO: Optional edge-scroll behavior can be enabled/tuned here later.
                HandleEdgeScroll();
            }
        }

        private void HandlePan()
        {
            float x = 0f;
            float z = 0f;

            if (global::UnityEngine.Input.GetKey(KeyCode.W)) z += 1f;
            if (global::UnityEngine.Input.GetKey(KeyCode.S)) z -= 1f;
            if (global::UnityEngine.Input.GetKey(KeyCode.D)) x += 1f;
            if (global::UnityEngine.Input.GetKey(KeyCode.A)) x -= 1f;

            var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 move = (planarRight * x) + (planarForward * z);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            transform.position += move * (panSpeed * Time.unscaledDeltaTime);
        }

        private void HandleZoom()
        {
            float wheel = global::UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.0001f)
            {
                return;
            }

            float target = _camera.orthographicSize - wheel * zoomSpeed;
            _camera.orthographicSize = Mathf.Clamp(target, minOrthographicSize, maxOrthographicSize);
        }

        private void HandleEdgeScroll()
        {
            float x = 0f;
            float z = 0f;
            Vector3 mouse = global::UnityEngine.Input.mousePosition;

            if (mouse.x <= edgeScrollPixels) x -= 1f;
            else if (mouse.x >= Screen.width - edgeScrollPixels) x += 1f;

            if (mouse.y <= edgeScrollPixels) z -= 1f;
            else if (mouse.y >= Screen.height - edgeScrollPixels) z += 1f;

            if (Mathf.Approximately(x, 0f) && Mathf.Approximately(z, 0f))
            {
                return;
            }

            var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 move = (planarRight * x) + (planarForward * z);

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            transform.position += move * (edgeScrollSpeed * Time.unscaledDeltaTime);
        }
    }
}

