using UnityEngine;
using UnityEngine.EventSystems;

namespace PackOpen
{
    /// <summary>
    /// Put on a full-screen Image (raycast target on): a tap anywhere opens the pack.
    /// The label hides while a sequence runs and comes back, gently pulsing, when it completes.
    /// </summary>
    public class TapToOpen : MonoBehaviour, IPointerClickHandler
    {
        public PackData pack;
        public PackOpenSequence sequence;
        [Tooltip("Where the pack flies in from. Falls back to the sequence's PackOrigin anchor.")]
        public Transform origin;
        public CanvasGroup label;
        [SerializeField] float pulseSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] float pulseMin = 0.55f;

        bool _idle = true;

        void OnEnable()
        {
            if (sequence == null) return;
            sequence.Started += OnStarted;
            sequence.Completed += OnCompleted;
        }

        void OnDisable()
        {
            if (sequence == null) return;
            sequence.Started -= OnStarted;
            sequence.Completed -= OnCompleted;
        }

        void OnStarted(PackOpenContext c) { _idle = false; if (label != null) label.alpha = 0f; }
        void OnCompleted(PackOpenContext c) { _idle = true; }

        void Update()
        {
            if (!_idle || label == null) return;
            label.alpha = Mathf.Lerp(pulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (sequence == null || !_idle) return;
            sequence.Begin(pack, origin);
        }
    }
}
