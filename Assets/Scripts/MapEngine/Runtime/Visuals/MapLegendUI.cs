using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.MapEngine
{
    public class MapLegendUI : MonoBehaviour
    {
        [Header("UI Panel Settings")]
        public GameObject legendPanel;

        private void Awake()
        {
            BuildLegendUI();
        }

        private void BuildLegendUI()
        {
            // Background Card
            Image bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.07f, 0.06f, 0.92f);

            RectTransform panelRect = GetComponent<RectTransform>();
            if (panelRect == null) panelRect = gameObject.AddComponent<RectTransform>();
            
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(20f, -20f);
            panelRect.sizeDelta = new Vector2(300f, 260f);

            // Title
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            Text titleText = titleGo.AddComponent<Text>();
            titleText.text = "📜 MAP LEGEND & NODE TYPES";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 15;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1.0f, 0.85f, 0.4f);
            titleText.alignment = TextAnchor.MiddleCenter;

            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -8f);
            titleRect.sizeDelta = new Vector2(0f, 28f);

            // Node Entries List
            string[] entries = new string[]
            {
                "⚔️ <b>Minor Enemy</b>: Regular monster encounter.",
                "💎 <b>Elite Enemy</b>: Mini-boss with rare rewards.",
                "🔥 <b>Rest Site</b>: Heal HP or upgrade cards.",
                "🎁 <b>Treasure</b>: Open relic & gold mystery chest.",
                "🛒 <b>Shop Merchant</b>: Buy cards & relics.",
                "👑 <b>Boss</b>: Final boss of this act!"
            };

            Color[] colors = new Color[]
            {
                new Color(0.95f, 0.35f, 0.35f),
                new Color(1.0f, 0.35f, 0.6f),
                new Color(0.35f, 0.9f, 0.45f),
                new Color(1.0f, 0.85f, 0.3f),
                new Color(0.3f, 0.85f, 1.0f),
                new Color(0.75f, 0.35f, 0.95f)
            };

            float yOffset = -38f;
            for (int i = 0; i < entries.Length; i++)
            {
                GameObject entryGo = new GameObject($"LegendEntry_{i}");
                entryGo.transform.SetParent(transform, false);
                Text text = entryGo.AddComponent<Text>();
                text.text = entries[i];
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 13;
                text.supportRichText = true;
                text.color = colors[i];
                text.alignment = TextAnchor.MiddleLeft;

                RectTransform rect = entryGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(14f, yOffset);
                rect.sizeDelta = new Vector2(-20f, 32f);

                yOffset -= 34f;
            }
        }
    }
}
