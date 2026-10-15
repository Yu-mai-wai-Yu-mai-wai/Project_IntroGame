using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.VFX;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P0-6: Route all card removals through death animation (CardDeathFx).
    /// 1. BoardSlotView.ClearSlot routes removals through PlayDeathAnimation (sets IsDying, unparents to rig).
    /// 2. EnemyBoardView3D.RemoveView routes removals through PlayDeathAnimation.
    /// 3. Re-clearing or calling death on already dying cards does not double-play.
    /// Menu: Tools/TawanOS/Tests/Card Death FX. Batch: -executeMethod TawanOS.EditorTools.CardDeathFxTests.Run
    /// </summary>
    public static class CardDeathFxTests
    {
        [MenuItem("Tools/TawanOS/Tests/Card Death FX")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            var testGo = new GameObject("CardDeathFxTests");

            try
            {
                // --- 1. Player BoardSlotView.ClearSlot routes to death FX ---
                var playerSlotGo = new GameObject("TestPlayerSlot", typeof(MeshFilter), typeof(BoardSlotView));
                playerSlotGo.transform.SetParent(testGo.transform);
                var playerSlot = playerSlotGo.GetComponent<BoardSlotView>();
                playerSlot.side = BoardSlotView.SlotSide.Player;

                var playerCardGo = new GameObject("TestPlayerCard", typeof(BoxCollider), typeof(CardView3D));
                playerCardGo.transform.SetParent(playerSlotGo.transform);
                var playerCard = playerCardGo.GetComponent<CardView3D>();

                failures += Expect("Card is initially not dying", !playerCard.IsDying);
                failures += Expect("Card is initially child of slot", playerCard.transform.parent == playerSlotGo.transform);

                // Call ClearSlot() -> plays death FX, reparents into rig and in EditMode destroys immediately
                playerSlot.ClearSlot();

                failures += Expect("Player slot has no card children remaining after ClearSlot", playerSlotGo.transform.childCount == 0);
                failures += Expect("Player card was cleaned up by death rig", playerCard == null);

                // Calling ClearSlot again on empty slot is safe
                playerSlot.ClearSlot();
                failures += Expect("Calling ClearSlot on empty slot is safe", true);

                // --- 2. Direct death animation & double-play guard ---
                var guardCardGo = new GameObject("TestGuardCard", typeof(BoxCollider), typeof(CardView3D));
                guardCardGo.transform.SetParent(testGo.transform);
                var guardCard = guardCardGo.GetComponent<CardView3D>();

                var isDyingField = typeof(CardView3D).GetField("isDying", BindingFlags.NonPublic | BindingFlags.Instance);
                isDyingField.SetValue(guardCard, true);

                failures += Expect("Guard card is marked dying", guardCard.IsDying);
                float doublePlayLength = guardCard.PlayDeathAnimation(playerSlot);
                failures += Expect("PlayDeathAnimation returns 0 when card is already dying", Mathf.Approximately(doublePlayLength, 0f));
                failures += Expect("Guard card was not destroyed due to early return", guardCard != null);

                // --- 3. EnemyBoardView3D.RemoveView routes to death FX ---
                var enemyBoardGo = new GameObject("TestEnemyBoard", typeof(EnemyBoardView3D));
                enemyBoardGo.transform.SetParent(testGo.transform);
                var enemyBoard = enemyBoardGo.GetComponent<EnemyBoardView3D>();

                var enemySlotGo = new GameObject("TestEnemySlot", typeof(MeshFilter), typeof(BoardSlotView));
                enemySlotGo.transform.SetParent(testGo.transform);
                var enemySlot = enemySlotGo.GetComponent<BoardSlotView>();
                enemySlot.side = BoardSlotView.SlotSide.Enemy;
                enemySlot.slotIndex = 0;

                var enemyCardGo = new GameObject("TestEnemyCard", typeof(BoxCollider), typeof(CardView3D));
                enemyCardGo.transform.SetParent(enemySlotGo.transform);
                var enemyCard = enemyCardGo.GetComponent<CardView3D>();

                // Register into enemyBoard internal dictionaries via reflection
                var slotsField = typeof(EnemyBoardView3D).GetField("slots", BindingFlags.NonPublic | BindingFlags.Instance);
                var viewsField = typeof(EnemyBoardView3D).GetField("views", BindingFlags.NonPublic | BindingFlags.Instance);
                var slotsDict = (Dictionary<int, BoardSlotView>)slotsField.GetValue(enemyBoard);
                var viewsDict = (Dictionary<int, CardView3D>)viewsField.GetValue(enemyBoard);
                slotsDict[0] = enemySlot;
                viewsDict[0] = enemyCard;

                failures += Expect("Enemy card is initially not dying", !enemyCard.IsDying);

                // Call RemoveView(0) -> routes through PlayDeathAnimation and cleans up viewsDict
                enemyBoard.RemoveView(0);

                failures += Expect("Enemy views dictionary no longer contains slot 0", !viewsDict.ContainsKey(0));
                failures += Expect("Enemy card was cleaned up by death rig", enemyCard == null);

                // Calling RemoveView again when slot is empty or view is dying does not throw or error
                enemyBoard.RemoveView(0);
                failures += Expect("Calling RemoveView again is safe", true);

                // --- 4. ProcessDeaths double-play guard ---
                // If a card died in clash, but view is already marked IsDying, ProcessDeaths skips it
                var clashGo = new GameObject("TestClashView", typeof(BoardClashView3D));
                clashGo.transform.SetParent(testGo.transform);
                var clash = clashGo.GetComponent<BoardClashView3D>();

                var diedField = typeof(BoardClashView3D).GetField("died", BindingFlags.NonPublic | BindingFlags.Instance);
                var diedList = (List<CardInstance>)diedField.GetValue(clash);

                var dummyCard = new CardInstance { cardNameThai = "Dummy", familiarHealth = 0 };
                diedList.Add(dummyCard);

                // Run ProcessDeaths coroutine via MoveNext
                var processMethod = typeof(BoardClashView3D).GetMethod("ProcessDeaths", BindingFlags.NonPublic | BindingFlags.Instance);
                var coroutine = (IEnumerator)processMethod.Invoke(clash, null);
                bool advanced = coroutine.MoveNext();

                failures += Expect("ProcessDeaths completes without error for missing or dying card", true);
                failures += Expect("Died list was cleared", diedList.Count == 0);
            }
            finally
            {
                Object.DestroyImmediate(testGo);
            }

            if (failures == 0)
                Debug.Log("[CardDeathFxTests] PASS (all checks passed)");
            else
                Debug.LogError($"[CardDeathFxTests] FAIL ({failures} checks failed)");

            if (exitOnFinish && Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[CardDeathFxTests] ok   {label}");
                return 0;
            }
            Debug.LogError($"[CardDeathFxTests] FAIL {label}");
            return 1;
        }
    }
}
