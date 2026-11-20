using UnityEngine;
using UnityEngine.EventSystems;

namespace PackOpen
{
    /// <summary>Tappable reward tile that starts the sequence.</summary>
    public class PackTile : MonoBehaviour, IPointerClickHandler
    {
        public PackData pack;
        public PackOpenSequence sequence;
        public PackOpenDebugPanel debugPanel; // optional: honors the Force Duplicate toggle

        public void OnPointerClick(PointerEventData eventData)
        {
            bool dup = debugPanel != null && debugPanel.ConsumeForceDuplicate();
            sequence.Begin(pack, transform, dup);
        }
    }
}
