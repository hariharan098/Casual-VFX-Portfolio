using System.Collections;
using UnityEngine;

namespace PackOpen.Stages
{
    /// <summary>Short pause so the revealed cards can be read. Hook idle animations or VFX here.</summary>
    public class HoldStage : PackOpenStageBase
    {
        [SerializeField] float duration = 0.8f;
        public UnityEngine.Events.UnityEvent onHoldStart;

        public override IEnumerator Play(PackOpenContext ctx)
        {
            onHoldStart?.Invoke();
            float t = 0f;
            while (t < duration && !ctx.Skip) { t += Time.deltaTime; yield return null; }
        }
    }
}
