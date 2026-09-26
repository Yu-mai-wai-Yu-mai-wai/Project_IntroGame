using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

namespace TawanOS.MapEngine
{
    public class MapScrollController : MonoBehaviour
    {
        [Header("Target & Orientation")]
        public Transform targetTransform;
        public MapConfigSO config;
        public float scrollSensitivity = 1.0f;
        public float inertiaDamping = 0.92f;

        [Header("3D Table Scroll Bounds")]
        // Camera X range = node X range (18.6 .. -15.5) shifted by lookAheadX.
        public float minScrollX = -18.6f;
        public float maxScrollX = 15.5f;
        [Tooltip("Camera sits this far ahead (toward -X, screen right) of the current floor so the glass lands on the left third.")]
        public float lookAheadX = 3f;

        [Header("3D Table Atmosphere (matches the Blender concept render)")]
        public Color fogColor = new Color(0.11f, 0.075f, 0.055f, 1f);
        public float fogDensity = 0.03f;
        public float sunIntensity = 0.5f;
        [Range(0f, 1f)] public float vignetteIntensity = 0.45f;

        private Vector3 lastMousePosition;
        private bool isDragging = false;
        private Vector3 velocity = Vector3.zero;

        private void Start()
        {
            if (targetTransform == null)
            {
                targetTransform = Camera.main != null ? Camera.main.transform : transform;
            }

            if (config != null && config.use3DTableMode && config.orientation == MapOrientation.LeftToRight)
            {
                // Camera looks toward -Z (into the tree line); pitch comes from config so framing is tuned in one asset.
                targetTransform.rotation = Quaternion.Euler(config.cameraAnglePitch, 180f, 0f);
                ApplyAtmosphere(targetTransform.GetComponent<Camera>());
            }
        }

        private void ApplyAtmosphere(Camera cam)
        {
            // Fog colour doubles as the clear colour so the far edge of the paper floor fades out instead of cutting to black.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) light.intensity = sunIntensity;
            }

            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fogColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var volume = new GameObject("MapAtmosphereVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var vignette = volume.profile.Add<Vignette>(true);
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(0.5f);
        }

        private void Update()
        {
            HandleDragScroll();
        }

        private void HandleDragScroll()
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
                velocity = Vector3.zero;
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                lastMousePosition = Input.mousePosition;

                Vector3 scrollVector = Vector3.zero;
                float sensitivity = scrollSensitivity * 0.02f;

                if (config != null && config.use3DTableMode)
                {
                    if (config.orientation == MapOrientation.LeftToRight)
                    {
                        // In LeftToRight 3D mode with flipped camera (facing -Z, Screen Right is -X):
                        // Dragging mouse to the left (delta.x < 0) moves the camera to the right on screen (-X).
                        scrollVector.x = delta.x * sensitivity;
                    }
                    else
                    {
                        // In BottomToTop 3D mode, mouse Y drag moves camera along Z axis
                        scrollVector.z = -delta.y * sensitivity;
                    }
                }
                else if (config != null && (config.orientation == MapOrientation.LeftToRight || config.orientation == MapOrientation.RightToLeft))
                {
                    scrollVector.x = -delta.x * sensitivity;
                }
                else
                {
                    scrollVector.y = -delta.y * sensitivity;
                }

                velocity = scrollVector;
                targetTransform.position += scrollVector;
                ClampPosition();
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }
            else if (!isDragging && velocity.sqrMagnitude > 0.0001f)
            {
                targetTransform.position += velocity;
                velocity *= inertiaDamping;
                ClampPosition();
            }
        }

        public void ScrollToFloor(int floorIndex)
        {
            // MapManager.Start may call this before our Start has resolved the camera.
            if (targetTransform == null && Camera.main != null) targetTransform = Camera.main.transform;
            if (config == null || targetTransform == null) return;

            if (config.use3DTableMode)
            {
                Vector3 targetCamPos;
                if (config.orientation == MapOrientation.LeftToRight)
                {
                    float targetX;
                    if (floorIndex < 0)
                    {
                        targetX = maxScrollX;
                    }
                    else if (floorIndex >= 7)
                    {
                        targetX = minScrollX;
                    }
                    else
                    {
                        float[] floorXs = { 13.55f, 9.65f, 5.80f, 2.15f, -2.93f, -7.62f, -11.54f, -15.47f };
                        targetX = floorXs[Mathf.Clamp(floorIndex, 0, floorXs.Length - 1)];
                    }
                    targetX = Mathf.Clamp(targetX - lookAheadX, minScrollX, maxScrollX);
                    targetCamPos = new Vector3(targetX, config.cameraHeightY, config.cameraZDistance);
                }
                else
                {
                    float targetZ = floorIndex * config.floorSpacingY;
                    targetCamPos = new Vector3(0f, config.cameraHeightY, targetZ - config.cameraZDistance);
                }
                velocity = Vector3.zero;
                targetTransform.DOKill();
                targetTransform.DOMove(targetCamPos, 0.6f).SetEase(Ease.OutCubic);
                return;
            }

            float targetCoord = floorIndex * config.floorSpacingY;
            Vector3 targetPos = targetTransform.position;

            switch (config.orientation)
            {
                case MapOrientation.TopToBottom:
                    targetPos.y = -targetCoord;
                    break;
                case MapOrientation.LeftToRight:
                    targetPos.x = targetCoord;
                    break;
                case MapOrientation.RightToLeft:
                    targetPos.x = -targetCoord;
                    break;
                case MapOrientation.BottomToTop:
                default:
                    targetPos.y = targetCoord;
                    break;
            }

            velocity = Vector3.zero;
            targetTransform.DOKill();
            targetTransform.DOMove(targetPos, 0.6f).SetEase(Ease.OutCubic);
        }

        private void ClampPosition()
        {
            if (config == null || targetTransform == null) return;

            if (config.use3DTableMode)
            {
                Vector3 cPos = targetTransform.position;
                if (config.orientation == MapOrientation.LeftToRight)
                {
                    cPos.x = Mathf.Clamp(cPos.x, minScrollX, maxScrollX);
                    cPos.y = config.cameraHeightY;
                    cPos.z = config.cameraZDistance;
                }
                else
                {
                    float maxFloorZ = (config.totalFloors - 1) * config.floorSpacingY;
                    cPos.x = 0f;
                    cPos.y = config.cameraHeightY;
                    cPos.z = Mathf.Clamp(cPos.z, -config.cameraZDistance - 3f, maxFloorZ - config.cameraZDistance + 3f);
                }
                targetTransform.position = cPos;
                return;
            }

            float maxFloorCoord = (config.totalFloors - 1) * config.floorSpacingY;
            Vector3 currentPos = targetTransform.position;

            switch (config.orientation)
            {
                case MapOrientation.TopToBottom:
                    currentPos.y = Mathf.Clamp(currentPos.y, -maxFloorCoord, 0f);
                    break;
                case MapOrientation.LeftToRight:
                    currentPos.x = Mathf.Clamp(currentPos.x, 0f, maxFloorCoord);
                    break;
                case MapOrientation.RightToLeft:
                    currentPos.x = Mathf.Clamp(currentPos.x, -maxFloorCoord, 0f);
                    break;
                case MapOrientation.BottomToTop:
                default:
                    currentPos.y = Mathf.Clamp(currentPos.y, 0f, maxFloorCoord);
                    break;
            }

            targetTransform.position = currentPos;
        }
    }
}
