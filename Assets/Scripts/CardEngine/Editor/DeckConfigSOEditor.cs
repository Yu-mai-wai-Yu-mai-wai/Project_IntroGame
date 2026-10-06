#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Deck inspector: see and edit a deck as "card x count" rows instead of one long object list.
    //  - +/- per card, remove, ping; add from a catalog dropdown or by dragging Card Data in
    //  - Card count, type / school breakdown and total Merit / Corruption at a glance
    //  - StarterDeckConfig seeds every new run (RunState), so this is the player's starting deck
    [CustomEditor(typeof(DeckConfigSO))]
    public class DeckConfigSOEditor : Editor
    {
        private const int RecommendedStartingSize = 8;
        private const string StarterDeckName = "StarterDeckConfig";

        private int addIndex;

        public override void OnInspectorGUI()
        {
            var deck = (DeckConfigSO)target;
            serializedObject.Update();

            EditorGUILayout.LabelField("Deck", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("deckName"), new GUIContent("Deck Name"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("deckId"), new GUIContent("Deck ID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultDrawCount"), new GUIContent("Draw Count"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHandSize"), new GUIContent("Max Hand Size"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawSummary(deck);
            EditorGUILayout.Space(4);
            DrawCardRows(deck);
            EditorGUILayout.Space(4);
            DrawAddCard(deck);
        }

        // ---------------------------------------------------------------- summary

        private static void DrawSummary(DeckConfigSO deck)
        {
            var cards = deck.startingCards.Where(c => c != null).ToList();
            EditorGUILayout.LabelField($"Cards ({cards.Count})", EditorStyles.boldLabel);

            if (deck.name == StarterDeckName)
            {
                var type = cards.Count == RecommendedStartingSize ? MessageType.Info : MessageType.Warning;
                EditorGUILayout.HelpBox($"This is the player's starting deck: every new run begins with these cards " +
                    $"(recommended {RecommendedStartingSize}). Card rewards and the shop add to the run's copy, not to this asset.", type);
            }

            int familiars = cards.Count(c => c.cardType == CardType.Familiar);
            int amulets = cards.Count(c => c.cardType == CardType.Amulet);
            int incantations = cards.Count(c => c.cardType == CardType.Incantation);
            int white = cards.Count(c => c.magicSchool == MagicSchool.WhiteMagic);
            int black = cards.Count - white;
            EditorGUILayout.LabelField(
                $"Familiar {familiars}  •  Amulet {amulets}  •  Incantation {incantations}     White {white}  •  Black {black}     " +
                $"Merit {cards.Sum(c => c.meritCost)}  •  Corruption {cards.Sum(c => c.corruptionGain)}",
                EditorStyles.miniLabel);
        }

        // ---------------------------------------------------------------- card rows

        private static void DrawCardRows(DeckConfigSO deck)
        {
            // Group duplicates, keep first-appearance order
            var groups = deck.startingCards.Where(c => c != null).GroupBy(c => c).ToList();
            if (groups.Count == 0)
            {
                EditorGUILayout.HelpBox("The deck is empty.", MessageType.Warning);
                return;
            }

            foreach (var group in groups)
            {
                var card = group.Key;
                int count = group.Count();

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                var dot = GUILayoutUtility.GetRect(8, 18, GUILayout.Width(8));
                EditorGUI.DrawRect(new Rect(dot.x, dot.y + 3, 6, 12), card.magicSchool == MagicSchool.WhiteMagic ? new Color(0.92f, 0.88f, 0.75f) : new Color(0.35f, 0.15f, 0.4f));

                string name = DisplayName(card);
                if (GUILayout.Button(name, EditorStyles.label, GUILayout.MinWidth(110))) EditorGUIUtility.PingObject(card);
                EditorGUILayout.LabelField(TypeLabel(card), EditorStyles.miniLabel, GUILayout.Width(78));
                EditorGUILayout.LabelField(CostLabel(card), EditorStyles.miniLabel, GUILayout.Width(62));

                if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(22))) Change(deck, card, -1);
                EditorGUILayout.LabelField($"x{count}", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(26));
                if (GUILayout.Button("+", EditorStyles.miniButtonMid, GUILayout.Width(22))) Change(deck, card, +1);
                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22))) Change(deck, card, -count);
                EditorGUILayout.EndHorizontal();
            }

            int missing = deck.startingCards.Count(c => c == null);
            if (missing > 0 && GUILayout.Button($"Remove {missing} empty slot(s)"))
            {
                Undo.RecordObject(deck, "Remove empty deck slots");
                deck.startingCards.RemoveAll(c => c == null);
                EditorUtility.SetDirty(deck);
            }
        }

        // Thai name as shown in game; English only when the Thai name is missing
        private static string DisplayName(CardDataSO card)
        {
            return string.IsNullOrEmpty(card.cardNameThai) ? card.cardNameEng : card.cardNameThai;
        }

        private static string TypeLabel(CardDataSO card)
        {
            return card.cardType switch
            {
                CardType.Familiar => "Familiar",
                CardType.Amulet => "Amulet",
                _ => "Incantation",
            };
        }

        private static string CostLabel(CardDataSO card)
        {
            return card.magicSchool == MagicSchool.WhiteMagic ? $"Merit {card.meritCost}" : $"Corrupt {card.corruptionGain}";
        }

        private static void Change(DeckConfigSO deck, CardDataSO card, int delta)
        {
            Undo.RecordObject(deck, "Edit deck");
            if (delta > 0)
            {
                int last = deck.startingCards.LastIndexOf(card);
                for (int i = 0; i < delta; i++) deck.startingCards.Insert(last + 1, card);
            }
            else
            {
                for (int i = 0; i < -delta; i++)
                {
                    int last = deck.startingCards.LastIndexOf(card);
                    if (last < 0) break;
                    deck.startingCards.RemoveAt(last);
                }
            }
            EditorUtility.SetDirty(deck);
            GUIUtility.ExitGUI();
        }

        // ---------------------------------------------------------------- add

        private void DrawAddCard(DeckConfigSO deck)
        {
            var catalog = LoadAllCards();
            if (catalog.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                var labels = catalog.Select(c => $"{TypeLabel(c)}/{DisplayName(c)}").ToArray();
                addIndex = Mathf.Clamp(EditorGUILayout.Popup("Add Card", addIndex, labels), 0, catalog.Count - 1);
                if (GUILayout.Button("Add", GUILayout.Width(50)))
                {
                    Undo.RecordObject(deck, "Add card to deck");
                    deck.startingCards.Add(catalog[addIndex]);
                    EditorUtility.SetDirty(deck);
                }
                EditorGUILayout.EndHorizontal();
            }

            var drop = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "Drag Card Data here to add", EditorStyles.helpBox);
            HandleDrop(drop, deck);
        }

        private static List<CardDataSO> LoadAllCards()
        {
            var catalog = CardCatalogSO.Load();
            var cards = catalog != null
                ? catalog.cards.Where(c => c != null).ToList()
                : AssetDatabase.FindAssets("t:CardDataSO").Select(g => AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).ToList();
            return cards.OrderBy(c => c.cardType).ThenBy(DisplayName).ToList();
        }

        private static void HandleDrop(Rect area, DeckConfigSO deck)
        {
            var e = Event.current;
            if (!area.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;

            var cards = DragAndDrop.objectReferences.OfType<CardDataSO>().ToList();
            DragAndDrop.visualMode = cards.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (e.type != EventType.DragPerform || cards.Count == 0) return;

            DragAndDrop.AcceptDrag();
            Undo.RecordObject(deck, "Add cards to deck");
            deck.startingCards.AddRange(cards);
            EditorUtility.SetDirty(deck);
            e.Use();
        }
    }
}
#endif
