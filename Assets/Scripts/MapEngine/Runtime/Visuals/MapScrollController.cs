using UnityEngine;
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
        public float minScrollX = -18.6f;
        public float maxScrollX = 15.5f;

        private Vector3 lastMousePosition;
        private bool isDragging = false;
        private Vector3 velocity = Vector3.zero;

        private void Start()
        {
            if (targetTransform == null)
            {
                targetTransform = Camera.main != null ? Camera.main.transform : transform;
            }
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
                        // In LeftToRight 3D mode, mouse X drag moves camera along X axis
                        scrollVector.x = -delta.x * sensitivity;
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
            if (config == null || targetTransform == null) return;

            if (config.use3DTableMode)
            {
                Vector3 targetCamPos;
                if (config.orientation == MapOrientation.LeftToRight)
                {
                    float targetX;
                    if (floorIndex < 0)
                    {
                        targetX = minScrollX;
                    }
                    else if (floorIndex >= 7)
                    {
                        targetX = maxScrollX;
                    }
                    else
                    {
                        float[] floorXs = { -13.55f, -9.65f, -5.80f, -2.15f, 2.90f, 7.60f, 11.54f, 15.47f };
                        targetX = floorXs[Mathf.Clamp(floorIndex, 0, floorXs.Length - 1)];
                    }
                    targetX = Mathf.Clamp(targetX, minScrollX, maxScrollX);
                    targetCamPos = new Vector3(targetX, config.cameraHeightY, -config.cameraZDistance);
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
                    cPos.z = -config.cameraZDistance;
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
