using UnityEngine;

namespace TawanOS.CardEngine
{
    // Keeps the hand (and every card parented under it) locked to the camera's view,
    // like a viewmodel/HUD element, so the cards always face the player.
    public class CameraAnchoredHand : MonoBehaviour
    {
        [Header("Camera Anchor")]
        public Camera targetCamera;

        [Header("Offset From Camera (right, up, forward)")]
        public Vector3 offset = new Vector3(0f, -2.4f, 10f);

        [Header("Facing")]
        public bool matchCameraRotation = true;
        public bool flipFacing = false;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            Transform camT = targetCamera.transform;
            transform.position = camT.position
                + camT.right * offset.x
                + camT.up * offset.y
                + camT.forward * offset.z;

            if (matchCameraRotation)
            {
                transform.rotation = flipFacing
                    ? camT.rotation * Quaternion.Euler(0f, 180f, 0f)
                    : camT.rotation;
            }
        }
    }
}
