using UnityEngine;

namespace TawanOS.CardEngine
{
    // Keeps a card's status tags (CardView3D) up to date. Statuses come and go at round end, from auras and from flips
    // without a damage event, so the tags are checked every frame; a card on the enemy's board has its CardView3D
    // switched off, so this check lives in a component of its own. Added by CardView3D when it builds its face.
    public class CardStatusTags : MonoBehaviour
    {
        private CardView3D view;

        private void Awake()
        {
            view = GetComponent<CardView3D>();
        }

        private void LateUpdate()
        {
            if (view != null) view.RefreshStatusTags();
        }
    }
}
