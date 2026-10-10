#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Confirmation in front of every menu that rebuilds a scene, prefab or asset from code. These tools
    /// overwrite hand-tuned content (a re-run of the combat setup replaced the dressed combat scene and the card
    /// prefab on 2026-10-07), so a click must never be enough. Editor: a dialog. Batch mode (agents, CI):
    /// refused unless the environment variable TAWANOS_ALLOW_SETUP=1 is set on purpose.
    ///
    /// Only the [MenuItem] wrappers call this. The underlying methods stay callable from tests and from other
    /// editor code that already asks the developer first.
    /// </summary>
    public static class SetupGuard
    {
        public const string BatchEnv = "TAWANOS_ALLOW_SETUP";

        /// <summary>Replaceable so tests can answer without opening a modal window.</summary>
        public static Func<string, string, bool> Dialog = (title, message) =>
            EditorUtility.DisplayDialog(title, message, "สร้างทับ", "ยกเลิก");

        public static bool Confirm(string tool)
        {
            if (Application.isBatchMode)
            {
                bool allowed = Environment.GetEnvironmentVariable(BatchEnv) == "1";
                if (!allowed) Debug.LogError($"[SetupGuard] '{tool}' refused in batch mode. Set {BatchEnv}=1 to allow it on purpose.");
                return allowed;
            }

            bool yes = Dialog("สร้างทับของที่มีอยู่?",
                $"\"{tool}\" จะสร้าง scene / prefab / asset ใหม่จากโค้ด และอาจทับของที่ตกแต่งไว้แล้ว\n\n" +
                "ถ้าไม่แน่ใจ ให้กด ยกเลิก แล้ว commit งานปัจจุบันก่อน");
            if (!yes) Debug.Log($"[SetupGuard] '{tool}' cancelled.");
            return yes;
        }
    }
}
#endif
