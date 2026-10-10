using TawanOS.CardEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Tells the ตำราไสยเวท book (<see cref="CardBookPanel"/>, in the card engine) which cards the player has found,
    /// from the collection kept across runs (<see cref="CardCollection"/>). Hooked before the first scene loads.
    /// </summary>
    public static class CardBookHooks
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            CardBookPanel.IsKnown = card => CardCollection.Current.Has(card);
        }
    }
}
