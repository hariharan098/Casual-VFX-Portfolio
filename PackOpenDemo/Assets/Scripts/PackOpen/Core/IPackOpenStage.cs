using System.Collections;
using UnityEngine;

namespace PackOpen
{
    /// <summary>
    /// A stage is one animation beat. Play must finish (yield break) when the beat is done.
    /// Always honor ctx.Skip: if true, jump to the end state and return quickly.
    /// </summary>
    public interface IPackOpenStage
    {
        IEnumerator Play(PackOpenContext ctx);
    }

    /// <summary>Base class so stages can sit in an Inspector slot. Derive from this for your own stages.</summary>
    public abstract class PackOpenStageBase : MonoBehaviour, IPackOpenStage
    {
        public abstract IEnumerator Play(PackOpenContext ctx);
    }
}
