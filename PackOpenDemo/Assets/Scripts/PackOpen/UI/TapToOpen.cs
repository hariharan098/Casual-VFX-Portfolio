using UnityEngine;
using UnityEngine.EventSystems;

namespace PackOpen
{
    /// <summary>
    /// Put on a full-screen Image (raycast target on): a tap anywhere opens the pack.
    /// Idle state is read from sequence.IsRunning every frame, so the label always comes back,
    /// even if a stage throws or a run is interrupted.
    /// </summary>
    public class TapToOpen : MonoBehaviour, IPointerClickHandler
    {
        public PackData pack;
        public PackOpenSequence sequence;
        [Tooltip("Where the pack flies in from. Falls back to the sequence's PackOrigin anchor.")]
        public Transform origin;
        public CanvasGroup label;
        [Tooltip("Tapping while a pack is opening skips to the end of the sequence.")]
        public bool skipOnTap;
        [SerializeField] float pulseSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] float pulseMin = 0.55f;

        public bool IsIdle => sequence != null && !sequence.IsRunning;

        void Update()
        {
            if (label == null) return;
            label.alpha = IsIdle
                ? Mathf.Lerp(pulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed))
                : 0f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (sequence == null) return;
            if (sequence.IsRunning) { if (skipOnTap) sequence.Skip(); return; }
            sequence.Begin(pack, origin);
        }
    }
}
