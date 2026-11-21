using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PackOpen
{
    /// <summary>
    /// Runs the stages in PackOpenStageId order. Each slot holds one stage component.
    /// Swap a slot's component to replace the placeholder with your own stage.
    /// </summary>
    public class PackOpenSequence : MonoBehaviour
    {
        [Serializable]
        public class StageSlot
        {
            public PackOpenStageId id;
            public PackOpenStageBase stage;
        }

        [SerializeField] PackOpenAnchors anchors;
        [SerializeField] TapInput tapInput;
        [SerializeField] List<StageSlot> slots = new List<StageSlot>();
        [SerializeField] bool doubleTapToSkip;
        [SerializeField] bool logEvents = true;

        public bool IsRunning { get; private set; }
        public PackOpenStageId? CurrentStage { get; private set; }
        public PackData LastPack { get; private set; }

        // Hook for anything else (audio, analytics, debug UI).
        public event Action<PackOpenContext> Started;
        public event Action<PackOpenStageId, PackOpenContext> StageStarted;
        public event Action<PackOpenStageId, PackOpenContext> StageFinished;
        public event Action<PackOpenContext> Completed;

        PackOpenContext _ctx;
        int _lastSeed;
        Transform _lastOrigin;

        void OnEnable()
        {
            if (tapInput != null) tapInput.DoubleTapped += OnDoubleTap;
        }

        void OnDisable()
        {
            if (tapInput != null) tapInput.DoubleTapped -= OnDoubleTap;
        }

        void OnDoubleTap()
        {
            if (doubleTapToSkip) Skip();
        }

        public void Skip()
        {
            if (_ctx != null) _ctx.Skip = true;
        }

        /// <summary>Start a new open. Ignored while a sequence is running.</summary>
        public bool Begin(PackData pack, Transform origin = null)
        {
            if (IsRunning) return false;
            if (pack == null)
            {
                Debug.LogError("[PackOpen] Begin called with no PackData. Assign the pack on the caller (debug panel / PackTile) or re-run PackOpen > Setup Project.", this);
                return false;
            }
            _lastSeed = Environment.TickCount;
            _lastOrigin = origin;
            LastPack = pack;
            StartRun(pack, origin, _lastSeed);
            return true;
        }

        /// <summary>Replays the previous open with identical cards. Great for tuning.</summary>
        public bool Replay()
        {
            if (IsRunning || LastPack == null) return false;
            StartRun(LastPack, _lastOrigin, _lastSeed);
            return true;
        }

        void StartRun(PackData pack, Transform origin, int seed)
        {
            var rng = new System.Random(seed);
            _ctx = new PackOpenContext
            {
                Pack = pack,
                Results = PackRoller.Roll(pack, rng),
                Anchors = anchors,
                Input = tapInput,
                PackOrigin = origin != null ? origin : (anchors != null ? anchors.packOrigin : null)
            };
            StartCoroutine(Run(_ctx));
        }

        IEnumerator Run(PackOpenContext ctx)
        {
            IsRunning = true;
            try
            {
                Log($"Begin pack={ctx.Pack.id} cards={ctx.Results.Count}");
                foreach (var r in ctx.Results)
                    Log($"  card {r.Card.displayName} rarity={r.Card.rarity}");
                Started?.Invoke(ctx);

                foreach (PackOpenStageId id in Enum.GetValues(typeof(PackOpenStageId)))
                {
                    var stage = FindStage(id);
                    if (stage == null) { Debug.LogWarning($"[PackOpen] No stage assigned for {id}"); continue; }

                    CurrentStage = id;
                    Log($"-> {id}");
                    StageStarted?.Invoke(id, ctx);
                    yield return stage.Play(ctx);
                    StageFinished?.Invoke(id, ctx);
                }
                Log("Complete");
                Completed?.Invoke(ctx);
            }
            finally
            {
                CurrentStage = null;
                IsRunning = false;
                _ctx = null;
            }
        }

        PackOpenStageBase FindStage(PackOpenStageId id)
        {
            foreach (var s in slots) if (s.id == id) return s.stage;
            return null;
        }

        void Log(string m) { if (logEvents) Debug.Log("[PackOpen] " + m); }

#if UNITY_EDITOR
        // Used by the editor setup tool to fill slots.
        public void EditorSetup(PackOpenAnchors a, TapInput t, List<StageSlot> s) { anchors = a; tapInput = t; slots = s; }
#endif
    }
}
