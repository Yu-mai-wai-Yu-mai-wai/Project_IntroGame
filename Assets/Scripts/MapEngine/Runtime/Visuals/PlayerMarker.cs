using System.Collections;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public class PlayerMarker : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private Vector3 offset = new Vector3(0, 0, -0.1f);

        private void Awake()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite == null)
            {
                sr.sprite = GetFallbackMarkerSprite();
            }
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

        public void SetPositionImmediate(Vector3 targetWorldPos)
        {
            transform.position = targetWorldPos + offset;
        }

        public void MoveToPosition(Vector3 targetWorldPos)
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(AnimateMovement(targetWorldPos + offset));
        }

        private IEnumerator AnimateMovement(Vector3 target)
        {
            while (Vector3.Distance(transform.position, target) > 0.01f)
            {
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * moveSpeed);
                yield return null;
            }
            transform.position = target;
        }
    }
}
