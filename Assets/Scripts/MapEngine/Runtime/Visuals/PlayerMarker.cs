using System.Collections;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public class PlayerMarker : MonoBehaviour
    {
        [Header("Movement & 3D Alignment Settings")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private Vector3 offset = new Vector3(0, 0.02f, 0);
        [SerializeField] private Vector3 modelLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 modelLocalRotation = new Vector3(0, 180f, 0);
        [SerializeField] private Vector3 defaultFacingDirection = new Vector3(-1f, 0f, 0f);
        [SerializeField] private Vector3 hoverArcOffset = Vector3.zero;
        [SerializeField] private float tiltAmount = 0f;

        private Coroutine moveCoroutine;

        private void Awake()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite == null && transform.childCount == 0)
            {
                sr.sprite = GetFallbackMarkerSprite();
            }
            ApplyModelLocalTransform();
            if (defaultFacingDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(defaultFacingDirection.normalized);
            }
        }

        private void Start()
        {
            ApplyModelLocalTransform();
            if (defaultFacingDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(defaultFacingDirection.normalized);
            }
        }

        private void OnValidate()
        {
            ApplyModelLocalTransform();
        }

        public void ApplyModelLocalTransform()
        {
            foreach (Transform child in transform)
            {
                if (child.name == "Hands3DModel" || child.name.ToLower().Contains("hands"))
                {
                    child.localRotation = Quaternion.Euler(modelLocalRotation);

                    // Find glass child mesh ("circle" / "Circle") inside Hands3DModel
                    Transform glassChild = FindGlassChild(child);
                    if (glassChild != null)
                    {
                        // Rotate glass position vector by modelLocalRotation to get true local offset
                        Vector3 glassLocalPos = Quaternion.Euler(modelLocalRotation) * glassChild.localPosition;
                        child.localPosition = new Vector3(-glassLocalPos.x, -glassLocalPos.y, -glassLocalPos.z) + modelLocalOffset;
                    }
                    else
                    {
                        child.localPosition = modelLocalOffset;
                    }
                }
            }
        }

        private Transform FindGlassChild(Transform parent)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t != parent && t.name.ToLower().Contains("circle"))
                {
                    return t;
                }
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position - offset, 0.4f);
        }

        private static Sprite fallbackMarkerSprite;
        private static Sprite GetFallbackMarkerSprite()
        {
            if (fallbackMarkerSprite == null)
            {
                int res = 64;
                Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
                Color[] colors = new Color[res * res];
                float radius = res * 0.4f;
                Vector2 center = new Vector2(res * 0.5f, res * 0.5f);

                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        colors[y * res + x] = dist <= radius ? Color.white : Color.clear;
                    }
                }
                tex.SetPixels(colors);
                tex.Apply();
                fallbackMarkerSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            }
            return fallbackMarkerSprite;
        }

        public void SetPositionImmediate(Vector3 targetWorldPos, Vector3? facingDirection = null)
        {
            ResetMarker();
            transform.position = targetWorldPos + offset;
            Vector3 dir = facingDirection.HasValue && facingDirection.Value.sqrMagnitude > 0.001f 
                ? facingDirection.Value 
                : defaultFacingDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }

        public void ResetMarker()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
                moveCoroutine = null;
            }
            if (defaultFacingDirection.sqrMagnitude > 0.001f)
            {
                Vector3 dir = defaultFacingDirection;
                dir.y = 0f;
                transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
            else
            {
                transform.localRotation = Quaternion.identity;
            }
            gameObject.SetActive(true);
            ApplyModelLocalTransform();

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }
        }

        public void MoveToPosition(Vector3 targetWorldPos)
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(Animate3DMovement(targetWorldPos + offset));
        }

        private IEnumerator Animate3DMovement(Vector3 target)
        {
            Vector3 startPos = transform.position;
            float journeyLength = Vector3.Distance(startPos, target);
            if (journeyLength <= 0.001f) yield break;

            float startTime = Time.time;
            float duration = Mathf.Clamp(journeyLength / moveSpeed, 0.3f, 1.2f);
            Quaternion startRot = transform.rotation;

            Vector3 moveDir = target - startPos;
            moveDir.y = 0f;
            Quaternion targetRot = moveDir.sqrMagnitude > 0.001f 
                ? Quaternion.LookRotation(moveDir.normalized) 
                : startRot;

            while (Time.time - startTime < duration)
            {
                float t = (Time.time - startTime) / duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Pure linear surface slide: flush against paper board without lifting
                float arcHeight = hoverArcOffset.y > 0 ? Mathf.Sin(smoothT * Mathf.PI) * hoverArcOffset.y : 0f;
                Vector3 currentPos = Vector3.Lerp(startPos, target, smoothT) + new Vector3(0, arcHeight, 0);
                transform.position = currentPos;

                // Smoothly rotate the hand to face the movement direction
                transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

                yield return null;
            }

            transform.position = target;
            transform.rotation = targetRot;
        }
    }
}
