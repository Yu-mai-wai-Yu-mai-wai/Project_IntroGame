using DG.Tweening;
using TMPro;
using UnityEngine;
using TawanOS.CardEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// The turn shown on the combat table itself (Game Designer, 13 Oct), replacing the top-left turn banner: a word
    /// lying on the wood between the two rows of card slots, readable from the player's seat, with a glowing arrow on
    /// each side of it pointing at whoever's turn it is. "เทิร์นอัญเชิญ" while familiars and amulets are laid
    /// (PlayerBoard / EnemyBoard), "เทิร์นท่องคาถา" while incantations are cast (PlayerSpell / EnemySpell); in the
    /// other phases (draw, clash, end of turn) word and arrows fade away. When the turn passes to the other side they
    /// fade out and come back pointing the new way; the arrows never turn on screen (Game Designer, 13 Oct). Created by
    /// CombatFeedbackBootstrap with the turn banner.
    /// </summary>
    public class TableTurnMarker : MonoBehaviour
    {
        public const string SummonWord = "เทิร์นอัญเชิญ";
        public const string ChantWord = "เทิร์นท่องคาถา";

        private const string ArrowShader = "TawanOS_FireArrow";

        // World units on the table
        private const float WordWidth = 12f;                  // room for the longer word
        private const float WordHeight = 1.8f;
        private const float WordSize = 13f;
        private const float ArrowGap = 1.0f;                  // from the end of the word to the arrow's centre
        private static readonly Vector2 ArrowSize = new Vector2(1.2f, 1.8f);
        private const float Lift = 0.02f;                     // above the slots, so nothing on the table hides it

        private static readonly Color PlayerColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color PlayerHot = new Color(1f, 0.96f, 0.75f);
        private static readonly Color EnemyColor = new Color(1f, 0.45f, 0.32f);
        private static readonly Color EnemyHot = new Color(1f, 0.75f, 0.55f);

        private TurnPhaseController turns;
        private TextMeshPro word;
        private Transform[] arrows;
        private Material[] arrowMaterials;
        private float shown;                                  // 0..1, tweened
        private bool towardPlayer = true;
        private bool combatOver;

        private void Start()
        {
            turns = TurnPhaseController.Instance;
            if (turns == null || !TryFindTableCentre(out Vector3 centre))
            {
                Destroy(gameObject);
                return;
            }
            Build(centre);

            turns.OnPhaseChanged += Refresh;
            if (CombatManager.Instance != null) CombatManager.Instance.OnCombatEnded += OnCombatEnded;
            Refresh(turns.CurrentPhase);
            Apply();
        }

        private void OnDestroy()
        {
            if (turns != null) turns.OnPhaseChanged -= Refresh;
            if (CombatManager.Instance != null) CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            if (arrowMaterials != null) foreach (var m in arrowMaterials) if (m != null) Destroy(m);
        }

        private void OnCombatEnded(bool victory)
        {
            combatOver = true;
            FadeTo(0f);
        }

        // Midway between the player's row of slots and the enemy's, on the table's surface
        private static bool TryFindTableCentre(out Vector3 centre)
        {
            Vector3 player = Vector3.zero, enemy = Vector3.zero;
            int np = 0, ne = 0;
            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (slot.name.Contains("Deck")) continue;
                if (slot.side == BoardSlotView.SlotSide.Player) { player += slot.transform.position; np++; }
                else { enemy += slot.transform.position; ne++; }
            }
            centre = Vector3.zero;
            if (np == 0 || ne == 0) return false;
            centre = (player / np + enemy / ne) * 0.5f + Vector3.up * Lift;
            return true;
        }

        private void Build(Vector3 centre)
        {
            transform.position = centre;
            // Lying flat: the readable face up, the top of the letters toward the enemy
            var flat = Quaternion.Euler(90f, 0f, 0f);

            var wordGo = new GameObject("TurnWord");
            wordGo.transform.SetParent(transform, false);
            wordGo.transform.localRotation = flat;
            word = wordGo.AddComponent<TextMeshPro>();
            var theme = UIThemeSO.Current;
            if (theme != null && (theme.titleFont != null || theme.bodyFont != null))
                word.font = theme.titleFont != null ? theme.titleFont : theme.bodyFont;
            word.fontSize = WordSize;
            word.alignment = TextAlignmentOptions.Center;
            word.enableWordWrapping = false;
            word.rectTransform.sizeDelta = new Vector2(WordWidth, WordHeight);
            word.outlineWidth = 0.18f;
            word.outlineColor = new Color32(25, 8, 4, 255);

            var shader = Resources.Load<Shader>(ArrowShader);
            arrows = new Transform[2];
            arrowMaterials = new Material[2];
            for (int i = 0; i < 2; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = i == 0 ? "ArrowLeft" : "ArrowRight";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, false);
                float side = i == 0 ? -1f : 1f;
                quad.transform.localPosition = new Vector3(side * (WordWidth * 0.5f + ArrowGap), 0f, 0f);
                quad.transform.localScale = new Vector3(ArrowSize.x, ArrowSize.y, 1f);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (shader != null)
                {
                    arrowMaterials[i] = new Material(shader);
                    renderer.sharedMaterial = arrowMaterials[i];
                }
                arrows[i] = quad.transform;
            }
        }

        private void Refresh(TurnPhase phase)
        {
            if (combatOver || word == null) return;
            bool summon = phase == TurnPhase.PlayerBoard || phase == TurnPhase.EnemyBoard;
            bool chant = phase == TurnPhase.PlayerSpell || phase == TurnPhase.EnemySpell;
            if (!summon && !chant)
            {
                FadeTo(0f);
                return;
            }

            bool player = phase == TurnPhase.PlayerBoard || phase == TurnPhase.PlayerSpell;
            string text = summon ? SummonWord : ChantWord;

            // A new word or a new side: fade out, swap (arrows now point the new way), fade back in
            if (shown > 0.01f && (word.text != text || towardPlayer != player))
            {
                DOTween.Kill(this);
                DOTween.Sequence().SetId(this).SetLink(gameObject)
                    .Append(DOVirtual.Float(shown, 0f, 0.2f, v => { shown = v; Apply(); }))
                    .AppendCallback(() => Set(text, player))
                    .Append(DOVirtual.Float(0f, 1f, 0.3f, v => { shown = v; Apply(); }));
            }
            else
            {
                Set(text, player);
                FadeTo(1f);
            }
        }

        private void Set(string text, bool player)
        {
            word.text = text;
            towardPlayer = player;
            // The arrows sit just beyond the ends of the word, however long it is
            float half = Mathf.Max(word.GetPreferredValues(text).x, 1f) * 0.5f;
            for (int i = 0; i < arrows.Length; i++)
                if (arrows[i] != null) arrows[i].localPosition = new Vector3((i == 0 ? -1f : 1f) * (half + ArrowGap), 0f, 0f);
            var c = player ? PlayerColor : EnemyColor;
            c.a = shown;
            word.color = c;
            foreach (var m in arrowMaterials)
            {
                if (m == null) continue;
                m.SetColor("_FlameColor", player ? PlayerColor : EnemyColor);
                m.SetColor("_TipColor", player ? PlayerHot : EnemyHot);
            }
        }

        private void FadeTo(float target)
        {
            DOTween.Kill(this);
            DOVirtual.Float(shown, target, 0.25f, v => { shown = v; Apply(); }).SetId(this).SetLink(gameObject);
        }

        private void Apply()
        {
            if (word == null) return;
            var c = word.color;
            c.a = shown;
            word.color = c;
            // Arrows lie flat, pointing toward the player (-Z) or the enemy (+Z)
            float angle = towardPlayer ? 180f : 0f;
            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i] == null) continue;
                arrows[i].localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, angle);
                if (arrowMaterials[i] != null) arrowMaterials[i].SetFloat("_Alpha", shown);
            }
        }
    }
}
