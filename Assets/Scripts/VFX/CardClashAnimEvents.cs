using UnityEngine;

namespace TawanOS.VFX
{
    // Receives the Animation Events of the card clash clips (it sits next to the Animator on the rig's pivot).
    // Put an "OnImpact" event in Impact / ImpactCrit where the hit should land: the damage is applied then.
    public class CardClashAnimEvents : MonoBehaviour
    {
        public const string ImpactEvent = "OnImpact";

        public bool ImpactFired { get; private set; }

        public void ResetImpact()
        {
            ImpactFired = false;
        }

        // Animation Event
        public void OnImpact()
        {
            ImpactFired = true;
        }
    }
}
