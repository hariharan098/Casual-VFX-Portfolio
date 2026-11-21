using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PackOpen
{
    /// <summary>Everything a stage needs. Created per run by PackOpenSequence.</summary>
    public class PackOpenContext
    {
        public PackData Pack;
        public List<CardResult> Results;
        public PackOpenAnchors Anchors;
        public TapInput Input;
        /// <summary>The tile the player tapped (fly-in start). Falls back to Anchors.packOrigin.</summary>
        public Transform PackOrigin;
        /// <summary>Set true when the player skips. Stages must finish immediately.</summary>
        public bool Skip;

        public Transform CardSlot(int i) =>
            Anchors != null && Anchors.cardSlots != null && i < Anchors.cardSlots.Length ? Anchors.cardSlots[i] : null;

        /// <summary>Use inside a stage: yield return ctx.WaitForTap();</summary>
        public IEnumerator WaitForTap() => Input.WaitForTap(this);
    }
}
