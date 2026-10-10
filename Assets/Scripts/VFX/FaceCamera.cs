using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// Turns a painted layer a little to keep facing the camera as the camera moves, so pictures seem to
    /// follow the player's look instead of sitting flat. <see cref="strength"/> 1 = always square to the
    /// camera, 0 = never turns. Runs after the camera has moved this frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class FaceCamera : MonoBehaviour
    {
        [Tooltip("1 = keeps facing the camera fully, 0.5 = turns half as much, 0 = stays still.")]
        [Range(0f, 2f)] public float strength = 1f;
        [Tooltip("Degrees the turn may never pass, so a layer's edge never shows.")]
        [Range(0f, 15f)] public float maxAngle = 6f;

        private Transform cam;
        private Quaternion restRotation;
        private Quaternion restLook;

        private void Start()
        {
            var c = Camera.main;
            if (c == null) { enabled = false; return; }
            cam = c.transform;
            restRotation = transform.rotation;
            restLook = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            var look = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
            var turn = look * Quaternion.Inverse(restLook);

            turn.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            angle = Mathf.Clamp(angle * strength, -maxAngle, maxAngle);
            transform.rotation = Quaternion.AngleAxis(angle, axis) * restRotation;
        }
    }
}
