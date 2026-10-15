using System.Reflection;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TawanOS.ShopEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P0-4: Shop menu stays open when right-clicking a card to open the book.
    /// Menu: Tools/TawanOS/Tests/Shop Right Click. Batch: -executeMethod TawanOS.EditorTools.ShopRightClickTests.Run
    /// </summary>
    public static class ShopRightClickTests
    {
        [MenuItem("Tools/TawanOS/Tests/Shop Right Click")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            var testGo = new GameObject("ShopRightClickTests");

            try
            {
                // 1. When CardDetailPanelUI / CardBookPanel is blocking input or just opened
                var bookGo = new GameObject("CardBookPanelTest");
                bookGo.transform.SetParent(testGo.transform);
                var book = bookGo.AddComponent<CardBookPanel>();
                typeof(CardBookPanel).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(book, null);

                var isOpenField = typeof(CardBookPanel).GetField("isOpen", BindingFlags.NonPublic | BindingFlags.Instance);
                var openedFrameField = typeof(CardBookPanel).GetField("openedFrame", BindingFlags.NonPublic | BindingFlags.Instance);
                var closedFrameField = typeof(CardBookPanel).GetField("closedFrame", BindingFlags.NonPublic | BindingFlags.Instance);

                // Initially not open
                isOpenField.SetValue(book, false);
                openedFrameField.SetValue(book, -1);
                closedFrameField.SetValue(book, -1);

                failures += Expect("When book is closed, ShouldSkipCloseOnRightClick is false without card hover",
                    !ShopShrineView.ShouldSkipCloseOnRightClick());

                // Set isOpen = true (book is open)
                isOpenField.SetValue(book, true);
                failures += Expect("When book is open, CardDetailPanelUI.BlocksInput is true",
                    CardDetailPanelUI.BlocksInput);
                failures += Expect("When book is open, ShopShrineView skips close on right-click",
                    ShopShrineView.ShouldSkipCloseOnRightClick());

                // Set isOpen = false, but closed this frame
                isOpenField.SetValue(book, false);
                closedFrameField.SetValue(book, Time.frameCount);
                failures += Expect("In frame book closed, CardDetailPanelUI.BlocksInput is still true",
                    CardDetailPanelUI.BlocksInput);
                failures += Expect("In frame book closed, ShopShrineView skips close on right-click",
                    ShopShrineView.ShouldSkipCloseOnRightClick());

                // Reset closed frame, but test just opened this frame
                closedFrameField.SetValue(book, -1);
                openedFrameField.SetValue(book, Time.frameCount);
                failures += Expect("In frame book opened, CardBookPanel.JustOpened is true",
                    CardBookPanel.JustOpened);
                failures += Expect("In frame book opened, CardDetailPanelUI.JustOpened is true",
                    CardDetailPanelUI.JustOpened);
                failures += Expect("In frame book opened, ShopShrineView skips close on right-click",
                    ShopShrineView.ShouldSkipCloseOnRightClick());

                // 2. Test pointer over card check with EventSystem
                openedFrameField.SetValue(book, -1); // reset

                var eventGo = new GameObject("EventSystem");
                eventGo.transform.SetParent(testGo.transform);
                var eventSystem = eventGo.AddComponent<EventSystem>();
                eventGo.AddComponent<StandaloneInputModule>();

                var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(testGo.transform);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var cardGo = new GameObject("RewardCard", typeof(RectTransform), typeof(Image), typeof(RewardCardView));
                cardGo.transform.SetParent(canvasGo.transform, false);
                var cardRect = cardGo.GetComponent<RectTransform>();
                cardRect.sizeDelta = new Vector2(200f, 300f);
                cardRect.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

                // Simulate pointer raycast hit on card
                var pointerData = new PointerEventData(eventSystem)
                {
                    position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, hits);

                bool hitCard = false;
                for (int i = 0; i < hits.Count; i++)
                {
                    if (hits[i].gameObject != null && hits[i].gameObject.GetComponentInParent<RewardCardView>() != null)
                        hitCard = true;
                }
                failures += Expect("Raycast hits RewardCardView at center", hitCard);
            }
            finally
            {
                Object.DestroyImmediate(testGo);
            }

            if (failures == 0)
                Debug.Log("[ShopRightClickTests] PASS (all checks passed)");
            else
                Debug.LogError($"[ShopRightClickTests] FAIL ({failures} checks failed)");

            if (exitOnFinish && Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[ShopRightClickTests] ok   {label}");
                return 0;
            }
            Debug.LogError($"[ShopRightClickTests] FAIL {label}");
            return 1;
        }
    }
}
