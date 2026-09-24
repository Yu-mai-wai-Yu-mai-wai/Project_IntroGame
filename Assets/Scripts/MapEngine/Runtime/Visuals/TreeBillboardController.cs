using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.MapEngine
{
    /// <summary>
    /// Cylindrical Billboard controller for background tree sprites.
    /// Rotates tree quads exclusively around the Y-axis to face the camera plane,
    /// keeping trunks strictly perpendicular to the ground plane without tilting.
    /// </summary>
    [ExecuteAlways]
    public class TreeBillboardController : MonoBehaviour
    {
        [Header("Camera & Trees")]
        public Camera targetCamera;
        [SerializeField] private List<Transform> treeTransforms = new List<Transform>();
        [SerializeField] private bool autoFindOnStart = true;
        [SerializeField] private bool invertForward = false;

        private void Awake()
        {
            EnsureCamera();
            if (autoFindOnStart && (treeTransforms == null || treeTransforms.Count == 0))
            {
                FindTrees();
            }
        }

        private void Start()
        {
            EnsureCamera();
            if (treeTransforms == null || treeTransforms.Count == 0)
            {
                FindTrees();
            }
        }

        private void EnsureCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        [ContextMenu("Find Trees In Scene/Hierarchy")]
        public void FindTrees()
        {
            if (treeTransforms == null)
            {
                treeTransforms = new List<Transform>();
            }
            treeTransforms.Clear();

            Transform root = transform;
            if (!HasTreeChildren(root))
            {
                GameObject env = GameObject.Find("MapNavigateEnvironment");
                if (env != null)
                {
                    root = env.transform;
                }
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root) continue;
                string cName = child.name;
                if (cName.StartsWith("Tree") || cName.StartsWith("tree") || cName.StartsWith("Plane"))
                {
                    treeTransforms.Add(child);
                }
            }

            Debug.Log($"[TreeBillboardController] Found {treeTransforms.Count} tree meshes under '{root.name}'.");
        }

        private bool HasTreeChildren(Transform parent)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child == parent) continue;
                string cName = child.name;
                if (cName.StartsWith("Tree") || cName.StartsWith("tree") || cName.StartsWith("Plane"))
                {
                    return true;
                }
            }
            return false;
        }

        private void LateUpdate()
        {
            EnsureCamera();
            if (targetCamera == null || treeTransforms == null || treeTransforms.Count == 0) return;

            Vector3 camPos = targetCamera.transform.position;

            for (int i = 0; i < treeTransforms.Count; i++)
            {
                Transform t = treeTransforms[i];
                if (t == null) continue;

                // Cylindrical Billboard: Match camera position on horizontal plane only
                Vector3 targetPos = new Vector3(camPos.x, t.position.y, camPos.z);
                Vector3 lookDir = invertForward ? (t.position - targetPos) : (targetPos - t.position);

                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    t.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                }
            }
        }
    }
}
