using System;
using System.Collections;
using UnityEngine;

namespace PackOpen
{
    /// <summary>Single place that detects taps (mouse or touch via the legacy Input Manager).</summary>
    public class TapInput : MonoBehaviour
    {
        [SerializeField] float doubleTapWindow = 0.25f;

        public int TapCount { get; private set; }
        public event Action Tapped;
        public event Action DoubleTapped;

        float _lastTapTime = -10f;

        void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            TapCount++;
            Tapped?.Invoke();
            if (Time.unscaledTime - _lastTapTime <= doubleTapWindow) DoubleTapped?.Invoke();
            _lastTapTime = Time.unscaledTime;
        }

        /// <summary>Waits for a NEW tap (taps before the call are ignored). Returns early on skip.</summary>
        public IEnumerator WaitForTap(PackOpenContext ctx = null)
        {
            int start = TapCount;
            while (TapCount == start && (ctx == null || !ctx.Skip)) yield return null;
        }
    }
}
