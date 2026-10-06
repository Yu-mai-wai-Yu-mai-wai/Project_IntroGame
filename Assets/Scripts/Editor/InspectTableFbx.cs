using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    public static class InspectTableFbx
    {
        [MenuItem("Tools/TawanOS/Debug/Inspect Table FBX")]
        public static void Inspect()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectAsset/CombatDemo/Table.fbx");
            if (go == null)
            {
                Debug.LogError("Table.fbx not found!");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== HIERARCHY ===");
            CollectHierarchy(go.transform, "", sb);
            Debug.Log($"[TableInspect] Hierarchy:\n{sb}");

            var subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/ProjectAsset/CombatDemo/Table.fbx");
            var meshSb = new System.Text.StringBuilder();
            meshSb.AppendLine("=== MESHES ===");
            foreach (var a in subAssets)
            {
                if (a is Mesh mesh)
                    meshSb.AppendLine($"Mesh: {mesh.name} ({mesh.vertexCount} verts)");
                else if (a is GameObject g)
                    meshSb.AppendLine($"GO: {g.name}");
            }
            Debug.Log($"[TableInspect] Assets:\n{meshSb}");
        }

        private static void CollectHierarchy(Transform t, string indent, System.Text.StringBuilder sb)
        {
            sb.AppendLine($"{indent}{t.name}");
            for (int i = 0; i < t.childCount; i++)
            {
                CollectHierarchy(t.GetChild(i), indent + "  ", sb);
            }
        }
    }
}
