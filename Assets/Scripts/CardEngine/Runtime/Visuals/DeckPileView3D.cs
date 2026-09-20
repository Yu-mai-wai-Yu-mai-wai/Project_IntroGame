using UnityEngine;

namespace TawanOS.CardEngine
{
    // Visual 3D draw pile lying on the table. Put it on the Player_DeckSlot object: the stack is
    // built at that slot's world position and its height follows CardManager.DrawPile.Count.
    // The stack is a standalone object (not a child) because the slot is tilted and non-uniformly
    // scaled, which would distort a child mesh.
    public class DeckPileView3D : MonoBehaviour
    {
        [Header("Pile Look")]
        public Vector2 cardSize = new Vector2(0.7f, 1f); // width (x), depth (z) of a card lying flat
        public float thicknessPerCard = 0.02f;
        public float minVisibleThickness = 0.02f;
        public Color pileColor = new Color(0.25f, 0.1f, 0.15f);
        public float heightLerpSpeed = 10f;

        private Transform pile;
        private float currentThickness;

        // Where drawn cards start flying from: the top face of the pile.
        public Vector3 DrawSpawnPosition => new Vector3(
            transform.position.x,
            transform.position.y + currentThickness + 0.05f,
            transform.position.z);

        private void Start()
        {
            var pileGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pileGo.name = "DeckPile3D";
            Destroy(pileGo.GetComponent<Collider>()); // must not intercept card mouse events
            pileGo.GetComponent<Renderer>().material.color = pileColor;
            pile = pileGo.transform;
            ApplyThickness(0f);
        }

        private void OnDestroy()
        {
            if (pile != null) Destroy(pile.gameObject);
        }

        private void LateUpdate()
        {
            if (pile == null) return;

            int count = CardManager.Instance != null ? CardManager.Instance.DrawPile.Count : 0;
            float target = count > 0 ? Mathf.Max(minVisibleThickness, count * thicknessPerCard) : 0f;
            ApplyThickness(Mathf.MoveTowards(currentThickness, target, heightLerpSpeed * Time.deltaTime));
        }

        private void ApplyThickness(float thickness)
        {
            currentThickness = thickness;
            pile.gameObject.SetActive(thickness > 0.0001f);
            pile.localScale = new Vector3(cardSize.x, Mathf.Max(thickness, 0.0001f), cardSize.y);
            pile.rotation = Quaternion.identity;
            pile.position = transform.position + Vector3.up * (thickness * 0.5f);
        }
    }
}
