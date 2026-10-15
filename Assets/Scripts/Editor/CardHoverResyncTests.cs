using System.Reflection;
using DG.Tweening;
using TawanOS.CardEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P0-3: Re-sync card hover after closing the card book.
    /// 1. Hand card unhovers when book closes if mouse is not over it.
    /// 2. Board card unhovers when book closes if mouse is not over it.
    /// Menu: Tools/TawanOS/Tests/Card Hover Resync. Batch: -executeMethod TawanOS.EditorTools.CardHoverResyncTests.Run
    /// </summary>
    public static class CardHoverResyncTests
    {
        [MenuItem("Tools/TawanOS/Tests/Card Hover Resync")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            var testGo = new GameObject("CardHoverResyncTests");

            try
            {
                // Create a test card
                var cardGo = new GameObject("TestHandCard", typeof(BoxCollider), typeof(CardView3D));
                cardGo.transform.SetParent(testGo.transform);
                var card = cardGo.GetComponent<CardView3D>();

                // Initialize private fields
                var baseScaleField = typeof(CardView3D).GetField("baseLocalScale", BindingFlags.NonPublic | BindingFlags.Instance);
                baseScaleField.SetValue(card, Vector3.one);
                card.SetRestingTransform(new Vector3(1f, 0f, 0f), Quaternion.identity, applyImmediately: true);

                // --- 1. Hand card hover and unhover ---
                failures += Expect("Initially not hovered", !card.IsHovered);

                // Hover
                card.Hover();
                DOTween.CompleteAll();
                failures += Expect("Card is hovered", card.IsHovered);
                failures += Expect("Card scaled up on hover", card.transform.localScale.x > 1f);

                // Simulate book open
                var bookGo = new GameObject("TestBook", typeof(CardBookPanel));
                bookGo.transform.SetParent(testGo.transform);
                var book = bookGo.GetComponent<CardBookPanel>();
                typeof(CardBookPanel).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(book, null);
                typeof(CardBookPanel).GetField("isOpen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(book, true);

                failures += Expect("IgnoresMouse is true when book is open", card.IgnoresMouse);

                // OnMouseExit called while book is open (returns early)
                typeof(CardView3D).GetMethod("OnMouseExit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(card, null);
                failures += Expect("Hover remains true because OnMouseExit returned early while IgnoresMouse was true", card.IsHovered);

                // Close book
                typeof(CardBookPanel).GetField("isOpen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(book, false);
                typeof(CardBookPanel).GetField("closedFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(book, -1);

                failures += Expect("IgnoresMouse is false after book closed", !card.IgnoresMouse);

                // Resync hover
                card.ResyncHover();
                DOTween.CompleteAll();
                failures += Expect("Hover is cleared after ResyncHover", !card.IsHovered);
                failures += Expect("Scale returned to baseLocalScale", Mathf.Approximately(card.transform.localScale.x, 1f));

                // --- 2. Board card hover and unhover ---
                var boardSlotGo = new GameObject("TestBoardSlot", typeof(MeshFilter), typeof(BoardSlotView));
                boardSlotGo.transform.SetParent(testGo.transform);
                var slot = boardSlotGo.GetComponent<BoardSlotView>();
                slot.side = BoardSlotView.SlotSide.Player;

                var boardCardGo = new GameObject("TestBoardCard", typeof(BoxCollider), typeof(CardView3D));
                boardCardGo.transform.SetParent(boardSlotGo.transform);
                var boardCard = boardCardGo.GetComponent<CardView3D>();

                baseScaleField.SetValue(boardCard, Vector3.one);
                typeof(CardView3D).GetField("isPlacedOnBoard", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boardCard, true);
                typeof(CardView3D).GetField("originalLocalPosition", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boardCard, Vector3.zero);
                typeof(CardView3D).GetField("originalLocalRotation", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boardCard, Quaternion.identity);

                failures += Expect("Board card is not ignoring mouse when idle", !boardCard.IgnoresMouse);

                boardCard.Hover();
                DOTween.CompleteAll();
                failures += Expect("Board card is hovered", boardCard.IsHovered);
                failures += Expect("Board card scaled up on hover", boardCard.transform.localScale.x > 1f);

                // Simulate book open
                typeof(CardBookPanel).GetField("isOpen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(book, true);
                failures += Expect("Board card ignores mouse while book is open", boardCard.IgnoresMouse);

                // OnMouseExit called while book open
                typeof(CardView3D).GetMethod("OnMouseExit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boardCard, null);
                failures += Expect("Board card hover stuck while book open", boardCard.IsHovered);

                // Close book & resync
                typeof(CardBookPanel).GetField("isOpen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(book, false);
                typeof(CardBookPanel).GetField("closedFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(book, -1);
                boardCard.ResyncHover();
                DOTween.CompleteAll();

                failures += Expect("Board card unhovered after book closed", !boardCard.IsHovered);
                failures += Expect("Board card scale restored to base scale", Mathf.Approximately(boardCard.transform.localScale.x, 1f));
            }
            finally
            {
                Object.DestroyImmediate(testGo);
            }

            if (failures == 0)
                Debug.Log("[CardHoverResyncTests] PASS (all checks passed)");
            else
                Debug.LogError($"[CardHoverResyncTests] FAIL ({failures} checks failed)");

            if (exitOnFinish && Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[CardHoverResyncTests] ok   {label}");
                return 0;
            }
            Debug.LogError($"[CardHoverResyncTests] FAIL {label}");
            return 1;
        }
    }
}
