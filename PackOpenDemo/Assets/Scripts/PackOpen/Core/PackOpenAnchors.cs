using UnityEngine;

namespace PackOpen
{
    /// <summary>Scene positions the stages animate between. Move or replace freely.</summary>
    public class PackOpenAnchors : MonoBehaviour
    {
        public Transform packOrigin;      // fallback when the tapped tile is not known
        public Transform packCenter;      // where the pack lands
        public Transform[] cardSlots;     // up to 3
        public Transform packExit;        // where the pack drops to
        public Transform walletTarget;    // Wallet icon the cards fly to
    }
}
