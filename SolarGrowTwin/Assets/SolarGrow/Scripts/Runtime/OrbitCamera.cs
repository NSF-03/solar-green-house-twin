using System;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Mouse camera: left-drag orbits, right- or middle-drag pans, the wheel zooms; keys 1/2 focus
    /// greenhouse A/B, F resets the view. Ignores the mouse over the panels. Uses the legacy Input class.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbitCamera : MonoBehaviour
    {
        [Tooltip("Controller whose houses keys 1 and 2 focus.")]
        public TwinController controller;

        [Tooltip("World point the camera orbits around.")]
        public Vector3 focus = new Vector3(2.3f, 1f, 0f);

        [Tooltip("Distance from the focus point (m).")]
        public float distance = 9f;

        [Tooltip("Horizontal angle (deg); 0 looks north (+Z).")]
        public float yaw = -20f;

        [Tooltip("Vertical angle (deg); positive looks down.")]
        public float pitch = 22f;

        [Tooltip("Orbit speed (deg per pixel of mouse movement).")]
        public float orbitSpeed = 0.3f;

        [Tooltip("Pan speed (fraction of the distance per pixel).")]
        public float panSpeed = 0.0015f;

        [Tooltip("Zoom step per wheel notch (fraction of the distance).")]
        [Range(0.01f, 0.5f)] public float zoomStep = 0.12f;

        [Tooltip("Closest zoom (m).")]
        public float minDistance = 1f;

        [Tooltip("Farthest zoom (m).")]
        public float maxDistance = 80f;

        [Tooltip("Lowest pitch (deg).")]
        public float minPitch = -10f;

        [Tooltip("Highest pitch (deg).")]
        public float maxPitch = 89f;

        [Tooltip("How fast the focus glides to a new target (1/s).")]
        public float focusSmoothing = 8f;

        Vector3 targetFocus;
        Vector3 lastMouse;
        int dragButton = -1;
        bool inputBroken;
        bool homeSet;
        Vector3 homeFocus;
        float homeDistance, homeYaw, homePitch;

        void Start()
        {
            targetFocus = focus;
            RememberHome();
            SnapToView();
        }

        void RememberHome()
        {
            homeFocus = focus;
            homeDistance = distance;
            homeYaw = yaw;
            homePitch = pitch;
            homeSet = true;
        }

        /// <summary>Set focus and distance and place the camera at once (works in edit mode too).</summary>
        public void SetView(Vector3 newFocus, float newDistance)
        {
            focus = newFocus;
            targetFocus = newFocus;
            distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
            RememberHome();
            SnapToView();
        }

        public void SnapToView()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = rot;
            transform.position = focus - rot * Vector3.forward * distance;
        }

        void Update()
        {
            if (inputBroken) return;
            try
            {
                HandleInput();
            }
            catch (InvalidOperationException e)
            {
                inputBroken = true;
                Debug.LogWarning("SolarGrow OrbitCamera: the legacy Input class is switched off. Set Edit > Project Settings > Player > " +
                                 "Active Input Handling to 'Both' or 'Input Manager (Old)'. " + e.Message);
            }
        }

        void HandleInput()
        {
            Vector3 mouse = Input.mousePosition;
            bool overUi = TwinUtil.IsPointerOverUi(mouse);
            if (dragButton < 0 && !overUi)
            {
                if (Input.GetMouseButtonDown(0)) dragButton = 0;
                else if (Input.GetMouseButtonDown(1)) dragButton = 1;
                else if (Input.GetMouseButtonDown(2)) dragButton = 2;
                if (dragButton >= 0) lastMouse = mouse;
            }
            if (dragButton >= 0)
            {
                if (!Input.GetMouseButton(dragButton))
                {
                    dragButton = -1;
                }
                else
                {
                    Vector3 d = mouse - lastMouse;
                    lastMouse = mouse;
                    if (dragButton == 0)
                    {
                        yaw += d.x * orbitSpeed;
                        pitch = Mathf.Clamp(pitch - d.y * orbitSpeed, minPitch, maxPitch);
                    }
                    else
                    {
                        targetFocus -= (transform.right * d.x + transform.up * d.y) * (panSpeed * distance);
                    }
                }
            }
            if (!overUi)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                    distance = Mathf.Clamp(distance * Mathf.Pow(1f - zoomStep, scroll), minDistance, maxDistance);
            }
            if (GUIUtility.keyboardControl == 0)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) FocusHouse(0);
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) FocusHouse(1);
                if (Input.GetKeyDown(KeyCode.F)) ResetView();
            }
        }

        void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-focusSmoothing * Time.unscaledDeltaTime);
            focus = Vector3.Lerp(focus, targetFocus, k);
            SnapToView();
        }

        /// <summary>Glide to house index (0 = A, 1 = B).</summary>
        public void FocusHouse(int index)
        {
            Vector3 c;
            if (controller != null && controller.TryGetHouseCenter(index, out c))
            {
                targetFocus = c;
                distance = Mathf.Clamp(5.5f, minDistance, maxDistance);
            }
        }

        public void ResetView()
        {
            if (!homeSet) return;
            targetFocus = homeFocus;
            distance = homeDistance;
            yaw = homeYaw;
            pitch = homePitch;
        }
    }
}
