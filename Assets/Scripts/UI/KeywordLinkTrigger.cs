using TMPro;
using UnityEngine;
using TawanOS.CardEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// Detects hover over &lt;link="kw:term"&gt; tags inside a TMP_Text (UI or 3D world text)
    /// and drives the KeywordTooltipView (Task P1-1).
    /// </summary>
    public class KeywordLinkTrigger : MonoBehaviour
    {
        // The trigger whose link the tooltip shows. Only it may hide the tooltip: with many texts on screen, one the
        // pointer just left used to hide the tooltip another had opened that same frame, and it never came back
        private static KeywordLinkTrigger owner;

        private TMP_Text textComponent;
        private Canvas parentCanvas;
        private int lastHoveredLink = -1;
        private string lastTerm;

        private void Awake()
        {
            textComponent = GetComponent<TMP_Text>();
            parentCanvas = GetComponentInParent<Canvas>();
        }

        private void Update()
        {
            if (textComponent == null || !textComponent.gameObject.activeInHierarchy) return;

            Camera cam = null;
            if (parentCanvas != null)
            {
                if (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : Camera.main;
            }
            else
            {
                cam = Camera.main;
                if (cam == null) cam = FindFirstObjectByType<Camera>();
            }

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, Input.mousePosition, cam);
            if (linkIndex != -1)
            {
                if (textComponent.textInfo != null && linkIndex < textComponent.textInfo.linkCount)
                {
                    var linkInfo = textComponent.textInfo.linkInfo[linkIndex];
                    string id = linkInfo.GetLinkID();
                    if (!string.IsNullOrEmpty(id) && id.StartsWith("kw:"))
                    {
                        string term = id.Substring(3);
                        if (linkIndex != lastHoveredLink || term != lastTerm || owner != this)
                        {
                            owner = this;
                            lastHoveredLink = linkIndex;
                            lastTerm = term;
                            string def = KeywordGlossarySO.Current.GetDefinition(term);
                            KeywordTooltipView.Instance.Show(term, def, Input.mousePosition);
                        }
                        else
                        {
                            KeywordTooltipView.Instance.UpdatePosition(Input.mousePosition);
                        }
                        return;
                    }
                }
            }

            if (lastHoveredLink != -1)
            {
                lastHoveredLink = -1;
                lastTerm = null;
                HideIfMine();
            }
        }

        private void HideIfMine()
        {
            if (owner != this) return;
            owner = null;
            KeywordTooltipView.Instance?.Hide();
        }

        private void OnDisable()
        {
            if (lastHoveredLink != -1)
            {
                lastHoveredLink = -1;
                lastTerm = null;
                HideIfMine();
            }
        }
    }
}
