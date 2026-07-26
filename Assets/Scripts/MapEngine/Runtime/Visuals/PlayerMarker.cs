using System.Collections;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public class PlayerMarker : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private Vector3 offset = new Vector3(0, 0, -0.1f);

        private Coroutine moveCoroutine;

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
