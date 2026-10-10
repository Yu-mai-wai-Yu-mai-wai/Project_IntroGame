using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Every menu that rebuilds a scene/prefab/asset sits behind SetupGuard: its [MenuItem] is on a "_Menu"
    /// wrapper, never on the raw method. Also checks the guard's yes/no behaviour.
    /// Menu: Tools/TawanOS/Tests/Setup Guard. Batch: -executeMethod TawanOS.EditorTools.SetupGuardTests.Run
    /// </summary>
    public static class SetupGuardTests
    {
        // Menu paths that overwrite existing content (see PLAN, 2026-10-07)
        private static readonly string[] Guarded =
        {
            "Tools/TawanOS/Card Engine/Convert Hand To 3D Cube Cards",
            "Tools/TawanOS/Card Engine/Setup Thai Fonts & Fallbacks",
            "Tools/TawanOS/Card Engine/Setup Test Scene & Cards",
            "Window/TawanOS Card Engine Setup",
            "Tools/TawanOS/Card Engine/Rebuild Card Prefab (Art Full Layout)",
            "Tools/TawanOS/Card Engine/Hook Up Graveyard (GraveyardZone)",
            "Tools/TawanOS/Combat Scene/Apply Blender Dressing",
            "Tools/TawanOS/Event Engine/Setup 5 Dark Fog Events",
            "Tools/TawanOS/Event Engine/Setup Event Scene & Sample Events",
            "Tools/TawanOS/Main Menu/Setup Main Menu Scene",
            "Tools/TawanOS/Meru/Setup Meru Scene",
            "Tools/TawanOS/Rewards/Setup Reward Scene",
            "Tools/TawanOS/Game Flow/Setup Victory Scene",
            "Tools/TawanOS/Shop Engine/Setup Shop Scene (Spirit House)",
            "Tools/TawanOS/Map Engine/Setup Test Scene & Profiles",
            "Window/TawanOS Map Engine Setup"
        };

        [MenuItem("Tools/TawanOS/Tests/Setup Guard")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            // 1. Every guarded menu path is on a *_Menu wrapper
            var found = new Dictionary<string, string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("Assembly-CSharp")) continue;
                foreach (var type in asm.GetTypes())
                    foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        foreach (var attr in m.GetCustomAttributes<MenuItem>())
                            found[attr.menuItem] = type.Name + "." + m.Name;
            }
            foreach (string path in Guarded)
            {
                bool exists = found.TryGetValue(path, out string method);
                failures += Expect($"menu exists: {path}", exists);
                if (exists) failures += Expect($"menu is guarded (wrapper name ends with _Menu): {method}", method.EndsWith("_Menu"));
            }

            // 2. Guard behaviour
            var oldDialog = SetupGuard.Dialog;
            try
            {
                string shown = null;
                SetupGuard.Dialog = (t, m) => { shown = m; return false; };
                failures += Expect("Confirm returns false when the developer cancels", !SetupGuard.Confirm("ทดสอบ"));
                failures += Expect("dialog names the tool", shown != null && shown.Contains("ทดสอบ"));
                SetupGuard.Dialog = (t, m) => true;
                failures += Expect("Confirm returns true when the developer accepts", SetupGuard.Confirm("ทดสอบ"));
            }
            finally { SetupGuard.Dialog = oldDialog; }

            if (failures == 0) Debug.Log("[SetupGuardTests] PASS");
            else Debug.LogError($"[SetupGuardTests] FAIL: {failures} check(s) failed.");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string name, bool ok)
        {
            if (ok) { Debug.Log($"  PASS {name}"); return 0; }
            Debug.LogError($"  FAIL {name}");
            return 1;
        }
    }
}
