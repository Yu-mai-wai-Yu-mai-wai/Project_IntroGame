using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// Look of the ตำราไสยเวท book (<see cref="CardBookPanel"/>): the pictures from Assets/Art/Book and the timings.
    /// Lives at Resources/CardBook so a build finds it; made by Tools/TawanOS/Main Menu/Create Card Book Style.
    /// </summary>
    [CreateAssetMenu(fileName = "CardBook", menuName = "TawanOS/Main Menu/Card Book Style")]
    public class CardBookStyleSO : ScriptableObject
    {
        [Header("Art")]
        [Tooltip("The open book: two pages side by side with the spine in the middle (Art/Book/Pages.png).")]
        public Sprite spread;
        [Tooltip("One loose page, used for the page that turns (Art/Book/Page.png).")]
        public Sprite page;
        [Tooltip("Back of a card, shown for cards not found yet (Art/Cards/FramedCard/back.png).")]
        public Sprite cardBack;
        [Tooltip("Leather colour of the binding that shows around the open pages.")]
        public Color bindingColor = new Color(0.36f, 0.22f, 0.13f);
        [Tooltip("Ink colour for the writing on the pages.")]
        public Color ink = new Color(0.12f, 0.07f, 0.03f);
        [Tooltip("Ink for headings of white magic cards.")]
        public Color whiteMagicInk = new Color(0.4f, 0.25f, 0.02f);
        [Tooltip("Ink for headings of black magic cards.")]
        public Color blackMagicInk = new Color(0.5f, 0.06f, 0.06f);

        [Header("Size")]
        [Tooltip("Height of the open book on a 1080p screen.")]
        public float bookHeight = 880f;
        [Tooltip("How far the binding shows around the pages, as a fraction of the book height.")]
        [Range(0f, 0.1f)] public float bindingMargin = 0.025f;
        [Tooltip("Height of a card on its page, as a fraction of the page height.")]
        [Range(0.3f, 0.95f)] public float cardHeight = 0.8f;

        [Header("Timing (seconds)")]
        [Tooltip("The book settling into view from the main menu.")]
        [Min(0.05f)] public float openDuration = 0.45f;
        [Tooltip("The book sliding up from the bottom when a card is opened in play.")]
        [Min(0.05f)] public float slideDuration = 0.4f;
        [Min(0.05f)] public float turnDuration = 0.5f;

        public static CardBookStyleSO Load() => Resources.Load<CardBookStyleSO>("CardBook");
    }
}
