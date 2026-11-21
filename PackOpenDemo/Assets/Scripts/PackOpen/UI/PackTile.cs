using UnityEngine;
using UnityEngine.EventSystems;

namespace PackOpen
{
    /// <summary>Tappable reward tile that starts the sequence.</summary>
    public class PackTile : MonoBehaviour, IPointerClickHandler
    {
        public PackData pack;
        public PackOpenSequence sequence;

        public void OnPointerClick(PointerEventData eventData)
        {
            sequence.Begin(pack, transform);
        }
    }
}
