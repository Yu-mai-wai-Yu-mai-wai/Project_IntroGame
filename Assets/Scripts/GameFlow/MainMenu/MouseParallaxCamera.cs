using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// The camera turns a little toward the mouse and slides a touch the other way, so the 3D stage behind a
    /// menu feels alive under the cursor without ever swinging hard. It eases back to its resting view when
    /// the window loses focus. Works on unscaled time, so it keeps moving under a paused game.
    /// <see cref="MainMenuUI"/> adds it to the main camera when the camera has none; add it to the camera
    /// yourself to tune the numbers in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class MouseParallaxCamera : MonoBehaviour
    {
        [Tooltip("Degrees the camera turns left/right with the mouse at the screen edge.")]
        [Range(0f, 10f)] public float maxYaw = 2.5f;
        [Tooltip("Degrees the camera turns up/down with the mouse at the screen edge.")]
        [Range(0f, 10f)] public float maxPitch = 1.5f;
        [Tooltip("How far (world units) the camera slides against the mouse, for depth. 0 = turn only.")]
        [Range(0f, 0.5f)] public float maxShift = 0.08f;
        [Tooltip("Seconds to catch up with the mouse. Higher = lazier, softer.")]
        [Range(0.05f, 2f)] public float smoothTime = 0.45f;

        private Quaternion restRotation;
        private Vector3 restPosition;
        private Vector2 current;  // -1..1 on both axes, smoothed
        private Vector2 velocity;

        private void Start()
        {
            restRotation = transform.localRotation;
            restPosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            Vector2 target = Vector2.zero;
            if (Application.isFocused && Screen.width > 0 && Screen.height > 0)
            {
                Vector3 mouse = Input.mousePosition;
                target = new Vector2(mouse.x / Screen.width * 2f - 1f, mouse.y / Screen.height * 2f - 1f);
                target = Vector2.ClampMagnitude(target, 1.2f);
                target.x = Mathf.Clamp(target.x, -1f, 1f);
                target.y = Mathf.Clamp(target.y, -1f, 1f);
            }

            current = Vector2.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            // Look toward the cursor (mouse up = look up), slide the other way for a little parallax.
            // Yaw turns around the world's up axis so the horizon never tilts on a camera that looks down.
            transform.localRotation = Quaternion.Euler(0f, current.x * maxYaw, 0f) * restRotation
                                      * Quaternion.Euler(-current.y * maxPitch, 0f, 0f);
            transform.localPosition = restPosition + restRotation * new Vector3(-current.x, -current.y, 0f) * maxShift;
        }

        private void OnDisable()
        {
            if (restRotation != default)
            {
                transform.localRotation = restRotation;
                transform.localPosition = restPosition;
            }
        }
    }
}
