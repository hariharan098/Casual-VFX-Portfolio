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

        public CollectionState Collection { get; } = new CollectionState();
        public bool IsRunning { get; private set; }
        public PackOpenStageId? CurrentStage { get; private set; }
        public PackData LastPack { get; private set; }

        // Hook for anything else (audio, analytics, debug UI).
        public event Action<PackOpenContext> Started;
        public event Action<PackOpenStageId, PackOpenContext> StageStarted;
        public event Action<PackOpenStageId, PackOpenContext> StageFinished;
        public event Action<PackOpenContext> Completed;

        PackOpenContext _ctx;
        HashSet<string> _snapshotBefore;
        int _lastSeed;
        CardData _lastForced;
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
        public bool Begin(PackData pack, Transform origin = null, bool forceDuplicate = false)
        {
            if (IsRunning) return false;
            CardData forced = null;
            if (forceDuplicate && pack.pool.Count > 0)
            {
                forced = pack.pool[UnityEngine.Random.Range(0, pack.pool.Count)];
                Collection.Add(forced); // already owned, so the first card is a duplicate
            }
            _snapshotBefore = Collection.Snapshot();
            _lastSeed = Environment.TickCount;
            _lastForced = forced;
            _lastOrigin = origin;
            LastPack = pack;
            StartRun(pack, origin, _lastSeed, forced);
            return true;
        }

        /// <summary>Replays the previous open with identical results (collection restored). Great for tuning.</summary>
        public bool Replay()
        {
            if (IsRunning || LastPack == null) return false;
            Collection.Restore(_snapshotBefore);
            StartRun(LastPack, _lastOrigin, _lastSeed, _lastForced);
            return true;
        }

        void StartRun(PackData pack, Transform origin, int seed, CardData forced)
        {
            var rng = new System.Random(seed);
            _ctx = new PackOpenContext
            {
                Pack = pack,
                Results = PackRoller.Roll(pack, Collection, rng, forced),
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
                Log($"Begin pack={ctx.Pack.id} variant={ctx.Variant} cards={ctx.Results.Count}");
                foreach (var r in ctx.Results)
                    Log($"  card {r.Card.displayName} rarity={r.Card.rarity} new={r.IsNew} stars={r.StarsFromDuplicate}");
                Started?.Invoke(ctx);

                foreach (PackOpenStageId id in Enum.GetValues(typeof(PackOpenStageId)))
                {
                    if (id == PackOpenStageId.DuplicateStars && !ctx.HasDuplicate) continue;
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
