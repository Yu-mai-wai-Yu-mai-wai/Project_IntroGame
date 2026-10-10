using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// ตำราไสยเวท: the book of every card, and the screen that shows one card in detail.
    /// The first spread is the title and a table of contents (white / black magic, each split into familiar, amulet
    /// and incantation). Each section opens with a spread of its own (its name and the list of its cards on the
    /// left, the right page kept blank for a write-up later), then every card has a spread, the card on the left
    /// page and its full information on the right (the same text the old detail screen showed). Pages turn one at a
    /// time (arrow keys, A/D, mouse wheel, the corner arrows or a click on the paper); contents and section lines
    /// jump to their page. From the main menu the book opens at the contents; <see cref="OpenAt"/> (a right-click on
    /// a card in play) slides the book up already open at that card, with its live values. Cards the player has not
    /// found yet lie face down. The look comes from <see cref="CardBookStyleSO"/> (Resources/CardBook).
    /// </summary>
    public class CardBookPanel : MonoBehaviour
    {
        public static CardBookPanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.isOpen;

        /// <summary>Open (or closed this frame): clicks should not also reach the table, the map or other screens.</summary>
        public static bool BlocksInput => Instance != null && (Instance.isOpen || Time.frameCount <= Instance.closedFrame);

        /// <summary>False when the book's art is missing (Resources/CardBook); callers fall back to their old screens.</summary>
        public static bool Available => CardBookStyleSO.Load() is { } s && s.spread != null && s.page != null;

        /// <summary>Which cards the player has found (set by the game flow); null = every card is shown face up.</summary>
        public static Func<CardDataSO, bool> IsKnown;

        public const string Title = "ตำราไสยเวท";

        private const float CardAspect = 939f / 1312f;

        // Where the paper sits inside the spread picture (fractions of Pages.png), and the gap kept inside each page
        private const float PaperX = 0.024f, PaperY = 0.036f;
        private const float PagePadX = 0.07f, PagePadY = 0.06f;

        // Book order: white magic first, each school as familiar, amulet, incantation
        private static readonly (MagicSchool school, CardType type)[] Sections =
        {
            (MagicSchool.WhiteMagic, CardType.Familiar), (MagicSchool.WhiteMagic, CardType.Amulet), (MagicSchool.WhiteMagic, CardType.Incantation),
            (MagicSchool.BlackMagic, CardType.Familiar), (MagicSchool.BlackMagic, CardType.Amulet), (MagicSchool.BlackMagic, CardType.Incantation),
        };

        private class Entry
        {
            public CardInstance card;
            public bool known;
            public int section;
        }

        private CardBookStyleSO style;
        private TMP_FontAsset font;
        private Sprite whiteFrame, blackFrame;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform book, leftPage, rightPage, turner;
        private Image turnerShade;
        private GameObject prevArrow, nextArrow;

        // What one spread shows: the contents (section -1), a section's opening page (entry -1) or a card
        private struct Spread
        {
            public int section;
            public int entry;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Spread> spreads = new List<Spread>();
        private readonly int[] sectionStart = new int[Sections.Length]; // spread of each section's opening page (-1 = no cards)
        private int spread;                                             // index into spreads; 0 = contents
        private bool isOpen, busy, slidIn;
        private int openedFrame = -1, closedFrame = -1;

        public static CardBookPanel Ensure()
        {
            if (Instance == null) new GameObject("CardBookPanel").AddComponent<CardBookPanel>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- open / close

        /// <summary>The whole book, open at the contents (main menu).</summary>
        public void Open()
        {
            if (!Prepare(null)) return;
            spread = 0;
            ShowSpread();
            StartCoroutine(Appear());
        }

        /// <summary>Slides the book up open at <paramref name="card"/>'s spread, showing that card's live values.</summary>
        public void OpenAt(CardInstance card)
        {
            if (card == null || !Prepare(card)) return;
            spread = SpreadOf(card);
            ShowSpread();
            StartCoroutine(SlideIn());
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            busy = true;
            closedFrame = Time.frameCount;
            group.blocksRaycasts = false;
            DOTween.Kill(this);
            var seq = DOTween.Sequence().SetId(this).SetUpdate(true).Append(group.DOFade(0f, 0.25f));
            if (slidIn) seq.Join(book.DOAnchorPosY(-1100f, 0.3f).SetEase(Ease.InCubic));
            else seq.Join(book.DOScale(0.92f, 0.25f).SetEase(Ease.InQuad));
            seq.OnComplete(() => { canvas.gameObject.SetActive(false); busy = false; });
        }

        // Fills the book from the card catalog; the card shown in play replaces its catalog page (it is known)
        private bool Prepare(CardInstance shown)
        {
            style = CardBookStyleSO.Load();
            if (style == null || style.spread == null) return false;
            FindLook();
            Build();
            EnsureEventSystem();

            entries.Clear();
            var catalog = CardCatalogSO.Load();
            bool shownPlaced = false;
            if (catalog != null)
            {
                foreach (var data in catalog.cards)
                {
                    if (data == null) continue;
                    bool isShown = shown != null && !shownPlaced && shown.cardId == data.cardId;
                    shownPlaced |= isShown;
                    entries.Add(new Entry
                    {
                        card = isShown ? shown : new CardInstance(data),
                        known = isShown || IsKnown == null || IsKnown(data),
                    });
                }
            }
            if (shown != null && !shownPlaced) entries.Add(new Entry { card = shown, known = true });

            foreach (var e in entries) e.section = SectionOf(e.card);
            entries.Sort((a, b) =>
            {
                int c = a.section.CompareTo(b.section);
                return c != 0 ? c : string.CompareOrdinal(a.card.cardId, b.card.cardId);
            });
            // Contents, then each section: its opening page followed by one spread per card
            spreads.Clear();
            spreads.Add(new Spread { section = -1, entry = -1 });
            for (int s = 0; s < Sections.Length; s++)
            {
                sectionStart[s] = -1;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].section != s) continue;
                    if (sectionStart[s] < 0)
                    {
                        sectionStart[s] = spreads.Count;
                        spreads.Add(new Spread { section = s, entry = -1 });
                    }
                    spreads.Add(new Spread { section = s, entry = i });
                }
            }

            // Active before the pages fill: text outlines need TextMeshPro's material, made when the text wakes
            DOTween.Kill(this);
            StopAllCoroutines();
            canvas.gameObject.SetActive(true);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            book.localScale = Vector3.one;
            book.anchoredPosition = Vector2.zero;
            isOpen = true;
            busy = true;
            openedFrame = Time.frameCount;
            return true;
        }

        private static int SectionOf(CardInstance card)
        {
            for (int i = 0; i < Sections.Length; i++)
                if (Sections[i].school == card.magicSchool && Sections[i].type == card.cardType) return i;
            return Sections.Length - 1;
        }

        private int SpreadOf(CardInstance card)
        {
            for (int i = 0; i < spreads.Count; i++)
                if (spreads[i].entry >= 0 && entries[spreads[i].entry].card == card) return i;
            return 0;
        }

        // Main menu: the open book settles into view at the contents
        private IEnumerator Appear()
        {
            slidIn = false;
            book.localScale = Vector3.one * 0.9f;
            float d = style.openDuration;
            var seq = DOTween.Sequence().SetId(this).SetUpdate(true)
                .Append(group.DOFade(1f, d * 0.5f))
                .Join(book.DOScale(1f, d).SetEase(Ease.OutBack));
            Sfx();
            yield return seq.WaitForCompletion();
            busy = false;
        }

        // In play: the open book rises from the bottom of the screen
        private IEnumerator SlideIn()
        {
            slidIn = true;
            book.anchoredPosition = new Vector2(0f, -1100f);
            var seq = DOTween.Sequence().SetId(this).SetUpdate(true)
                .Append(group.DOFade(1f, style.slideDuration * 0.5f))
                .Join(book.DOAnchorPosY(0f, style.slideDuration).SetEase(Ease.OutCubic));
            Sfx();
            yield return seq.WaitForCompletion();
            busy = false;
        }

        // ---------------------------------------------------------------- input

        private void Update()
        {
            if (!isOpen || canvas == null || Time.frameCount <= openedFrame) return;

            if (Input.GetMouseButtonDown(1) || EscapeKey.Use()) { Close(); return; }
            if (busy) return;

            float wheel = Input.mouseScrollDelta.y;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.PageDown) || wheel < -0.1f) TurnTo(spread + 1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.PageUp) || wheel > 0.1f) TurnTo(spread - 1);
            else if (Input.GetKeyDown(KeyCode.Home)) TurnTo(0);
        }

        private bool ClickAllowed => isOpen && !busy;

        private int LastSpread => spreads.Count - 1;

        // ---------------------------------------------------------------- turning pages

        /// <summary>Turns one page forward (+1) or back (-1), unless a turn is already under way.</summary>
        public void TurnPage(int direction) => TurnTo(spread + direction);

        /// <summary>Turns to <paramref name="target"/> in one sweep of the page (0 = contents).</summary>
        public void TurnTo(int target)
        {
            if (busy || !isOpen || target == spread || target < 0 || target > LastSpread) return;
            StartCoroutine(TurnAnimation(target));
        }

        // Forward: the right page lifts at its outer edge, folds over the spine and lands as the new left page.
        // The new right page is already underneath; the old left page is covered when the sheet lands.
        private IEnumerator TurnAnimation(int target)
        {
            busy = true;
            bool forward = target > spread;
            int from = spread;
            spread = target;
            Sfx();

            SetTurner(onRight: forward, from, leftSide: !forward);
            if (forward) FillPage(rightPage, target, leftSide: false); else FillPage(leftPage, target, leftSide: true);

            float half = style.turnDuration * 0.5f;
            turnerShade.color = new Color(0f, 0f, 0f, 0f);
            var first = DOTween.Sequence().SetId(this).SetUpdate(true)
                .Append(turner.DOScaleX(0f, half).SetEase(Ease.InQuad))
                .Join(turnerShade.DOFade(0.35f, half));
            yield return first.WaitForCompletion();

            SetTurner(onRight: !forward, target, leftSide: forward);
            turner.localScale = new Vector3(0f, 1f, 1f);
            turnerShade.color = new Color(0f, 0f, 0f, 0.35f);
            var second = DOTween.Sequence().SetId(this).SetUpdate(true)
                .Append(turner.DOScaleX(1f, half).SetEase(Ease.OutQuad))
                .Join(turnerShade.DOFade(0f, half));
            yield return second.WaitForCompletion();

            if (forward) FillPage(leftPage, target, leftSide: true); else FillPage(rightPage, target, leftSide: false);
            turner.gameObject.SetActive(false);
            RefreshArrows();
            busy = false;
        }

        // The turning sheet covers one half of the spread, hinged at the spine, showing one page of a spread
        private void SetTurner(bool onRight, int spreadIndex, bool leftSide)
        {
            turner.gameObject.SetActive(true);
            turner.SetAsLastSibling();
            turner.anchorMin = new Vector2(onRight ? 0.5f : PaperX, PaperY);
            turner.anchorMax = new Vector2(onRight ? 1f - PaperX : 0.5f, 1f - PaperY);
            turner.pivot = new Vector2(onRight ? 0f : 1f, 0.5f);
            turner.offsetMin = turner.offsetMax = Vector2.zero;
            turner.localScale = Vector3.one;

            // The page content keeps the place it has on the book, re-expressed inside the sheet
            var area = onRight ? rightPage : leftPage;
            var content = (RectTransform)turner.Find("Content");
            float x0 = turner.anchorMin.x, x1 = turner.anchorMax.x, y0 = turner.anchorMin.y, y1 = turner.anchorMax.y;
            content.anchorMin = new Vector2((area.anchorMin.x - x0) / (x1 - x0), (area.anchorMin.y - y0) / (y1 - y0));
            content.anchorMax = new Vector2((area.anchorMax.x - x0) / (x1 - x0), (area.anchorMax.y - y0) / (y1 - y0));
            content.offsetMin = content.offsetMax = Vector2.zero;
            FillPage(content, spreadIndex, leftSide);
            turnerShade.transform.SetAsLastSibling();
        }

        private void ShowSpread()
        {
            FillPage(leftPage, spread, leftSide: true);
            FillPage(rightPage, spread, leftSide: false);
            turner.gameObject.SetActive(false);
            RefreshArrows();
        }

        private void RefreshArrows()
        {
            prevArrow.SetActive(spread > 0);
            nextArrow.SetActive(spread < LastSpread);
        }

        // ---------------------------------------------------------------- page content

        private void FillPage(RectTransform page, int spreadIndex, bool leftSide)
        {
            for (int i = page.childCount - 1; i >= 0; i--) Destroy(page.GetChild(i).gameObject);

            var s = spreads[spreadIndex];
            if (s.section < 0)
            {
                if (leftSide) TitlePage(page); else ContentsPage(page);
            }
            else if (s.entry < 0)
            {
                if (leftSide) SectionPage(page, s.section, spreadIndex);
                // the right page of a section opening is left blank for its write-up later
            }
            else if (leftSide) CardPage(page, entries[s.entry], spreadIndex);
            else InfoPage(page, entries[s.entry]);
        }

        // A section's opening page: its name like a chapter title, then every card in it with its page
        private void SectionPage(RectTransform page, int section, int spreadIndex)
        {
            var (school, type) = Sections[section];

            var schoolText = NewText("School", page, 32, TextAlignmentOptions.Center);
            schoolText.color = SchoolInk(school);
            Place(schoolText.rectTransform, 0.84f, 0.92f);
            schoolText.text = school == MagicSchool.WhiteMagic ? "การ์ดมนต์ขาว" : "การ์ดมนต์ดำ";

            var title = NewText("Title", page, 76, TextAlignmentOptions.Center);
            title.color = style.ink;
            if (UIThemeSO.Current.titleFont != null) { title.font = UIThemeSO.Current.titleFont; title.fontStyle = FontStyles.Normal; }
            Place(title.rectTransform, 0.68f, 0.85f);
            title.text = NameOf(type);

            Rule(page, 0.665f, 0.2f, 0.8f);

            float y = 0.62f;
            for (int i = 0; i < spreads.Count; i++)
            {
                if (spreads[i].section != section || spreads[i].entry < 0) continue;
                var entry = entries[spreads[i].entry];
                int target = i;

                var row = NewText("Card", page, 28, TextAlignmentOptions.Left);
                row.textWrappingMode = TextWrappingModes.NoWrap;
                row.color = entry.known ? style.ink : Faded(style.ink, 0.5f);
                PlaceRow(row.rectTransform, y, 0.065f, 0.12f, 0.92f);
                row.text = $"{(entry.known ? entry.card.cardNameThai : "???")}<pos=72%>หน้า {target}";
                row.raycastTarget = true;
                var button = row.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => { if (ClickAllowed) TurnTo(target); });
                row.gameObject.AddComponent<CardBookHover>().ink = row;
                y -= 0.07f;
            }

            ContentsLink(page);
            PageNumber(page, spreadIndex);
        }

        // "« สารบัญ" at the top right of a page: back to the contents
        private void ContentsLink(RectTransform page)
        {
            var contents = NewText("Contents", page, 26, TextAlignmentOptions.Right);
            contents.color = style.ink;
            Place(contents.rectTransform, 0.94f, 1f);
            contents.text = "« สารบัญ";
            contents.raycastTarget = true;
            var back = contents.gameObject.AddComponent<Button>();
            back.transition = Selectable.Transition.None;
            back.onClick.AddListener(() => { if (ClickAllowed) TurnTo(0); });
            contents.gameObject.AddComponent<CardBookHover>().ink = contents;
        }

        private void PageNumber(RectTransform page, int spreadIndex)
        {
            var number = NewText("Page", page, 24, TextAlignmentOptions.Center);
            number.color = style.ink;
            Place(number.rectTransform, -0.02f, 0.04f);
            number.text = $"— {spreadIndex} —";
        }

        private void TitlePage(RectTransform page)
        {
            int found = 0;
            foreach (var e in entries) if (e.known) found++;

            var title = NewText("Title", page, 76, TextAlignmentOptions.Center);
            title.color = style.ink;
            if (UIThemeSO.Current.titleFont != null) { title.font = UIThemeSO.Current.titleFont; title.fontStyle = FontStyles.Normal; }
            Place(title.rectTransform, 0.6f, 0.82f);
            title.text = Title;

            Rule(page, 0.58f, 0.2f, 0.8f);

            var count = NewText("Count", page, 32, TextAlignmentOptions.Center);
            count.color = style.ink;
            Place(count.rectTransform, 0.45f, 0.56f);
            count.text = $"บันทึกแล้ว {found} / {entries.Count} ใบ";

            var hint = NewText("Hint", page, 22, TextAlignmentOptions.Center);
            hint.color = Faded(style.ink, 0.75f);
            Place(hint.rectTransform, 0.06f, 0.3f);
            hint.text = "เลือกหมวดจากสารบัญทางขวา\nพลิกหน้า: ← → หรือหมุนลูกกลิ้งเมาส์\nปิดตำรา: Esc หรือคลิกขวา";
        }

        // สารบัญ: each school, then its three kinds of card with the page they start on
        private void ContentsPage(RectTransform page)
        {
            var heading = NewText("Heading", page, 48, TextAlignmentOptions.Center);
            heading.color = style.ink;
            if (UIThemeSO.Current.titleFont != null) { heading.font = UIThemeSO.Current.titleFont; heading.fontStyle = FontStyles.Normal; }
            Place(heading.rectTransform, 0.86f, 0.98f);
            heading.text = "สารบัญ";
            Rule(page, 0.85f, 0.25f, 0.75f);

            float y = 0.8f;
            for (int s = 0; s < Sections.Length; s++)
            {
                var (school, _) = Sections[s];
                if (s == 0 || Sections[s - 1].school != school)
                {
                    var schoolText = NewText("School", page, 34, TextAlignmentOptions.Left);
                    schoolText.color = SchoolInk(school);
                    PlaceRow(schoolText.rectTransform, y, 0.08f, 0.04f, 0.96f);
                    schoolText.text = school == MagicSchool.WhiteMagic ? "การ์ดมนต์ขาว" : "การ์ดมนต์ดำ";
                    y -= 0.09f;
                }
                ContentsRow(page, s, y);
                y -= 0.075f;
                if (s == 2) y -= 0.04f;
            }
        }

        private void ContentsRow(RectTransform page, int section, float y)
        {
            int count = 0;
            foreach (var e in entries) if (e.section == section) count++;
            int start = sectionStart[section];

            var row = NewText("Row", page, 28, TextAlignmentOptions.Left);
            row.textWrappingMode = TextWrappingModes.NoWrap; // the page number stays on its line
            row.color = start > 0 ? style.ink : Faded(style.ink, 0.45f);
            PlaceRow(row.rectTransform, y, 0.07f, 0.14f, 0.96f);
            string kind = NameOf(Sections[section].type);
            row.text = start > 0 ? $"{kind} <size=70%>({count} ใบ)</size><pos=70%>หน้า {start}" : $"{kind} <size=70%>(ยังไม่มี)</size>";

            if (start <= 0) return;
            row.raycastTarget = true;
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { if (ClickAllowed) TurnTo(start); });
            row.gameObject.AddComponent<CardBookHover>().ink = row;
        }

        // Left page: the section it belongs to and a way back to the contents, the card large, the page number
        private void CardPage(RectTransform page, Entry entry, int spreadIndex)
        {
            // The section name goes back to the section's opening page
            var section = NewText("Section", page, 26, TextAlignmentOptions.Left);
            section.color = SchoolInk(entry.card.magicSchool);
            Place(section.rectTransform, 0.94f, 1f);
            section.text = $"{NameOf(entry.card.magicSchool)}  •  {NameOf(entry.card.cardType)}";
            int sectionPage = sectionStart[entry.section];
            if (sectionPage > 0)
            {
                section.raycastTarget = true;
                var toSection = section.gameObject.AddComponent<Button>();
                toSection.transition = Selectable.Transition.None;
                toSection.onClick.AddListener(() => { if (ClickAllowed) TurnTo(sectionPage); });
                section.gameObject.AddComponent<CardBookHover>().ink = section;
            }

            ContentsLink(page);

            var holder = NewRect("Card", page);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            float cardH = PageSize().y * style.cardHeight;
            holder.sizeDelta = new Vector2(cardH * CardAspect, cardH);
            holder.anchoredPosition = new Vector2(0f, -PageSize().y * 0.01f);

            // The shadow has the card's own shape (its picture, darkened), so the rounded corners match
            var shadow = NewImage("Shadow", holder, new Color(0f, 0f, 0f, 0.35f));
            shadow.sprite = entry.known && entry.card.artwork != null ? entry.card.artwork : style.cardBack;
            Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(5f, -9f);
            shadow.rectTransform.offsetMax = new Vector2(5f, -9f);

            if (entry.known) CardFace(holder, entry.card);
            else HiddenFace(holder);

            PageNumber(page, spreadIndex);
        }

        // Right page: everything about the card, in ink
        private void InfoPage(RectTransform page, Entry entry)
        {
            Color accent = SchoolInk(entry.card.magicSchool);
            string muted = ColorUtility.ToHtmlStringRGB(Color.Lerp(style.ink, new Color(0.72f, 0.64f, 0.46f), 0.3f)); // ink thinned a little toward the paper

            var header = NewText("Header", page, 34, TextAlignmentOptions.TopLeft);
            header.color = style.ink;
            Place(header.rectTransform, 0.78f, 0.98f);

            var body = NewText("Body", page, 28, TextAlignmentOptions.TopLeft);
            body.color = style.ink;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 18f;
            body.fontSizeMax = 30f;
            Place(body.rectTransform, 0.03f, 0.74f);

            if (!entry.known)
            {
                header.text = "<size=60>???</size>";
                body.text = $"<color=#{muted}>ยังไม่พบการ์ดใบนี้\nเมื่อได้พบในการเดินทาง ตำราจะบันทึกไว้เอง</color>";
            }
            else
            {
                header.text = CardDetailPanelUI.InfoHeader(entry.card, accent, muted, 0.9f);
                body.text = CardDetailPanelUI.InfoBody(entry.card, accent, muted,
                    ColorUtility.ToHtmlStringRGB(style.ink), "1F5A2C", "7A1414", 1.1f);
            }
            Rule(page, 0.76f, 0f, 1f);
        }

        // The card picture, with cost, attack, khwan and the ability written on it (as on the 3D card)
        private void CardFace(RectTransform holder, CardInstance card)
        {
            Sprite frame = CardFaceLayout.FaceFrame(card.cardImage, card.cardBackground, card.artwork, whiteFrame, blackFrame, card.magicSchool);
            bool printed = card.cardImage != null;

            if (!printed && card.artwork != null)
            {
                var art = NewImage("Artwork", holder, Color.white);
                art.sprite = card.artwork;
                PlaceOnFace(art.rectTransform, CardFaceLayout.Artwork);
            }
            if (frame != null)
            {
                var frameImage = NewImage("Frame", holder, Color.white);
                frameImage.sprite = frame;
                Stretch(frameImage.rectTransform);
            }

            if (!printed)
            {
                FaceText("Cost", holder, CardFaceLayout.Cost, 44).text =
                    (card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost : card.corruptionGain).ToString();
                if (!CardFaceLayout.ArtCarriesFrame(card.artwork))
                    FaceText("Name", holder, CardFaceLayout.Name, 32).text = card.cardNameThai;
                var ability = FaceText("Description", holder, CardFaceLayout.Description, 22);
                ability.textWrappingMode = TextWrappingModes.Normal;
                string format = card.descriptionFormat ?? "";
                try { ability.text = string.Format(format, card.baseValue); } catch { ability.text = format; }
            }
            if (card.cardType == CardType.Familiar || card.maxKhwan > 0)
            {
                if (card.cardType == CardType.Familiar) FaceText("Attack", holder, CardFaceLayout.Attack, 32).text = card.familiarDamage.ToString();
                FaceText("Khwan", holder, CardFaceLayout.Khwan, 32).text = card.familiarHealth.ToString();
            }
        }

        private void HiddenFace(RectTransform holder)
        {
            var back = NewImage("Back", holder, style.cardBack != null ? new Color(0.55f, 0.5f, 0.5f) : UIThemeSO.Current.black);
            back.sprite = style.cardBack;
            Stretch(back.rectTransform);
            var mark = NewText("Mark", holder, 72, TextAlignmentOptions.Center);
            Stretch(mark.rectTransform);
            mark.color = new Color(0.95f, 0.9f, 0.85f, 0.6f);
            CardFaceLayout.MakeReadable(mark);
            mark.text = "???";
        }

        private TextMeshProUGUI FaceText(string name, RectTransform face, CardFaceLayout.Box box, float maxSize)
        {
            var t = NewText(name, face, maxSize, TextAlignmentOptions.Center);
            PlaceOnFace(t.rectTransform, box);
            t.enableAutoSizing = true;
            t.fontSizeMin = 8f;
            t.fontSizeMax = maxSize;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.fontStyle = FontStyles.Normal;
            t.color = Color.white;
            CardFaceLayout.MakeReadable(t);
            return t;
        }

        private void Rule(RectTransform page, float y, float x0, float x1)
        {
            var rule = NewImage("Rule", page, Faded(style.ink, 0.45f));
            rule.rectTransform.anchorMin = new Vector2(x0, y);
            rule.rectTransform.anchorMax = new Vector2(x1, y);
            rule.rectTransform.offsetMin = new Vector2(0f, -1f);
            rule.rectTransform.offsetMax = new Vector2(0f, 1f);
        }

        private Color SchoolInk(MagicSchool school) => school == MagicSchool.WhiteMagic ? style.whiteMagicInk : style.blackMagicInk;

        private static Color Faded(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        // Size of one page's content area in canvas units
        private Vector2 PageSize()
        {
            var size = book.rect.size;
            return new Vector2(size.x * (leftPage.anchorMax.x - leftPage.anchorMin.x), size.y * (leftPage.anchorMax.y - leftPage.anchorMin.y));
        }

        // Thai names shown in the inspector ([InspectorName] on the enums)
        private static string NameOf(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attr = field != null ? (InspectorNameAttribute)Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) : null;
            return attr != null ? attr.displayName : value.ToString();
        }

        private static void Sfx()
        {
            TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_card_draw");
        }

        // ---------------------------------------------------------------- building the UI

        private void FindLook()
        {
            if (font == null) font = UIThemeSO.Current.bodyFont;
            var catalog = CardCatalogSO.Load();
            if (catalog != null)
            {
                if (whiteFrame == null) whiteFrame = catalog.defaultWhiteFrame;
                if (blackFrame == null) blackFrame = catalog.defaultBlackFrame;
            }
        }

        private void Build()
        {
            if (canvas != null) return;

            var canvasGo = new GameObject("CardBookCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // over the deck viewer and graveyard screens it can be opened from
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGo.GetComponent<CanvasGroup>();

            // Dimmed room behind the book: a click on it closes the book
            var dim = NewImage("Dim", canvasGo.transform, new Color(0.03f, 0.02f, 0.02f, 0.82f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ClickAllowed) Close(); });

            // The book: a leather binding behind, the open spread on top
            float aspect = style.spread.rect.width / style.spread.rect.height;
            book = NewRect("Book", canvasGo.transform);
            book.anchorMin = book.anchorMax = new Vector2(0.5f, 0.5f);
            book.sizeDelta = new Vector2(style.bookHeight * aspect, style.bookHeight);

            float m = style.bindingMargin * style.bookHeight;
            var binding = NewImage("Binding", book, style.bindingColor);
            binding.sprite = style.page; // plain paper texture, darkened to leather
            binding.raycastTarget = true; // clicks on the book edge do not close it
            Stretch(binding.rectTransform);
            binding.rectTransform.offsetMin = new Vector2(-m, -m);
            binding.rectTransform.offsetMax = new Vector2(m, m);

            var spreadImage = NewImage("Pages", book, Color.white);
            spreadImage.sprite = style.spread;
            spreadImage.raycastTarget = true;
            Stretch(spreadImage.rectTransform);

            leftPage = PageArea("LeftPage", left: true);
            rightPage = PageArea("RightPage", left: false);
            PageClick(leftPage, -1);
            PageClick(rightPage, +1);

            // The sheet that turns, hinged at the spine
            var turnerImage = NewImage("TurningPage", book, Color.white);
            turnerImage.sprite = style.page;
            turner = turnerImage.rectTransform;
            Stretch(NewRect("Content", turner));
            turnerShade = NewImage("Shade", turner, Color.clear);
            Stretch(turnerShade.rectTransform);
            turner.gameObject.SetActive(false);

            prevArrow = Arrow("Prev", "‹", left: true);
            nextArrow = Arrow("Next", "›", left: false);

            var close = NewText("Close", book, 26, TextAlignmentOptions.Right);
            close.color = new Color(0.9f, 0.82f, 0.7f);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(1f, 1f);
            close.rectTransform.pivot = new Vector2(1f, 0f);
            close.rectTransform.anchoredPosition = new Vector2(0f, m + 6f);
            close.rectTransform.sizeDelta = new Vector2(300f, 40f);
            close.raycastTarget = true;
            close.text = "ปิด (Esc)";
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ClickAllowed) Close(); });

            canvasGo.SetActive(false);
        }

        // One page's content area, inside the paper and clear of the spine
        private RectTransform PageArea(string name, bool left)
        {
            var rt = NewRect(name, book);
            float inner = 0.5f - PaperX;
            float x0 = left ? PaperX + inner * PagePadX : 0.5f + inner * PagePadX;
            float x1 = left ? 0.5f - inner * PagePadX : 1f - PaperX - inner * PagePadX;
            float y0 = PaperY + (1f - 2f * PaperY) * PagePadY, y1 = 1f - PaperY - (1f - 2f * PaperY) * PagePadY;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // A click on the paper (not on a contents line or button) turns that way, like taking the page corner
        private void PageClick(RectTransform page, int direction)
        {
            var hit = NewImage("Hit", book, Color.clear);
            hit.raycastTarget = true;
            hit.rectTransform.anchorMin = page.anchorMin;
            hit.rectTransform.anchorMax = page.anchorMax;
            hit.rectTransform.offsetMin = hit.rectTransform.offsetMax = Vector2.zero;
            hit.transform.SetSiblingIndex(page.GetSiblingIndex()); // under the page content, so its buttons take their own clicks
            hit.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ClickAllowed) TurnPage(direction); });
        }

        private GameObject Arrow(string name, string glyph, bool left)
        {
            var t = NewText(name, book, 90, TextAlignmentOptions.Center);
            t.color = style.ink;
            t.raycastTarget = true;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(left ? PaperX + 0.035f : 1f - PaperX - 0.035f, PaperY + 0.06f);
            rt.sizeDelta = new Vector2(90f, 90f);
            t.text = glyph;
            t.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ClickAllowed) TurnPage(left ? -1 : +1); });
            t.gameObject.AddComponent<CardBookHover>();
            return t.gameObject;
        }

        private static void Place(RectTransform rt, float y0, float y1)
        {
            rt.anchorMin = new Vector2(0f, y0);
            rt.anchorMax = new Vector2(1f, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void PlaceRow(RectTransform rt, float yTop, float height, float x0, float x1)
        {
            rt.anchorMin = new Vector2(x0, yTop - height);
            rt.anchorMax = new Vector2(x1, yTop);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void PlaceOnFace(RectTransform rt, CardFaceLayout.Box box)
        {
            Vector2 c = box.center + new Vector2(0.5f, 0.5f);
            rt.anchorMin = c - box.size * 0.5f;
            rt.anchorMax = c + box.size * 0.5f;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.fontStyle = FontStyles.Bold; // ink on paper reads better heavy; the card face and titles set their own
            UiFactory.EnableThaiMarks(t);
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    /// <summary>A contents line or page arrow in the book: lifts a little under the cursor and, if it has ink, goes red.</summary>
    public class CardBookHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TMP_Text ink;
        private Color rest;

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.DOScale(1.06f, 0.15f).SetUpdate(true).SetLink(gameObject);
            if (ink != null) { rest = ink.color; ink.color = new Color(0.6f, 0.08f, 0.08f, rest.a); }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.DOScale(1f, 0.15f).SetUpdate(true).SetLink(gameObject);
            if (ink != null) ink.color = rest;
        }
    }
}
