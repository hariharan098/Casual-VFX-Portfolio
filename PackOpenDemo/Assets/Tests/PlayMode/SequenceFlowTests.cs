using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PackOpen.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PackOpen.Tests
{
    /// <summary>Runs the demo scene end to end with placeholder stages and checks the hook order.</summary>
    public class SequenceFlowTests
    {
        const string ScenePath = "Assets/Scenes/PackOpenDemo.unity";

        [UnityTest]
        public IEnumerator Pack_PlaysAllStagesInOrder_WithoutTap()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var seq = Object.FindFirstObjectByType<PackOpenSequence>();
            Assert.NotNull(seq);
            var pack = AssetDatabase.LoadAssetAtPath<PackData>("Assets/Data/Packs/Pack_blue.asset");
            var order = new List<PackOpenStageId>();
            PackOpenContext ctx = null;
            seq.StageStarted += (id, c) => { order.Add(id); ctx = c; };

            // Snapshots taken at stage boundaries.
            int dealt = -1, dealtActive = -1, facesUp = -1;
            seq.StageFinished += (id, c) =>
            {
                var cr = Object.FindFirstObjectByType<CardsRig>();
                if (cr == null) return;
                if (id == PackOpenStageId.CardsMoveOut)
                {
                    dealt = cr.Cards.Count;
                    dealtActive = 0;
                    foreach (var cv in cr.Cards) if (cv.gameObject.activeSelf) dealtActive++;
                }
                if (id == PackOpenStageId.CardsFlip)
                {
                    facesUp = 0;
                    foreach (var cv in cr.Cards) if (cv.IsFront) facesUp++;
                }
            };

            var vfxAll = Object.FindObjectsByType<UiVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            UiVfx idle = null, fast = null;
            foreach (var v in vfxAll) { if (v.name == "VFX_CardPack_Idle") idle = v; if (v.name == "VFX_CardPack_Fast") fast = v; }
            Assert.NotNull(idle, "Idle VFX missing: run PackOpen > Setup Pack Rig");
            Assert.NotNull(fast, "Fast VFX missing: run PackOpen > Setup Pack Rig");
            Assert.IsFalse(idle.IsActive); Assert.IsFalse(fast.IsActive);
            var rigRef = Object.FindFirstObjectByType<PackRig>();
            float tearStart = -1f, tearEnd = -1f, dyAtLateral = float.NaN, maxDx = 0f;
            seq.StageStarted += (id, c) => { if (id == PackOpenStageId.PackTear) tearStart = Time.time; };
            seq.StageFinished += (id, c) => { if (id == PackOpenStageId.PackTear) tearEnd = Time.time; };
            bool idleSeen = false, fastSeen = false, idleDuringAppear = false;

            Assert.IsTrue(seq.Begin(pack));
            float t = 0;
            while (seq.IsRunning && t < 25f)
            {
                t += Time.deltaTime;
                idleSeen |= idle.IsActive; fastSeen |= fast.IsActive;
                if (seq.CurrentStage == PackOpenStageId.PackAppear) idleDuringAppear |= idle.IsActive;
                if (seq.CurrentStage == PackOpenStageId.PackTear && rigRef.strip.gameObject.activeSelf)
                {
                    var d = rigRef.strip.rectTransform.anchoredPosition3D - rigRef.StripHome;
                    maxDx = Mathf.Max(maxDx, Mathf.Abs(d.x));
                    if (float.IsNaN(dyAtLateral) && Mathf.Abs(d.x) >= 200f) dyAtLateral = d.y;
                }
                yield return null;
            }
            Assert.Greater(tearEnd - tearStart, 2.0f, "tear should be slow (wobble + rip + lift-off)");
            Assert.IsFalse(float.IsNaN(dyAtLateral), "strip should travel sideways");
            Assert.Less(dyAtLateral, 100f, "strip should be mostly lateral before it lifts off");
            Assert.Greater(maxDx, 600f, "strip should end far to the side");
            Assert.IsTrue(idleDuringAppear, "idle VFX should show while the pack comes in");
            Assert.IsTrue(idleSeen && fastSeen, "both VFX should have played");
            float w = 0;
            while ((idle.IsActive || fast.IsActive) && w < 6f) { w += Time.deltaTime; yield return null; }
            Assert.IsFalse(idle.IsActive, "idle VFX should be off after the run");
            Assert.IsFalse(fast.IsActive, "tear VFX should be off after the run");
            Assert.IsFalse(seq.IsRunning, "sequence did not finish on its own");

            var expected = new List<PackOpenStageId>
            {
                PackOpenStageId.PackAppear, PackOpenStageId.PackTear, PackOpenStageId.PackMoveDown,
                PackOpenStageId.CardsMoveOut, PackOpenStageId.CardsFlip, PackOpenStageId.Hold, PackOpenStageId.FlyToWallet
            };
            CollectionAssert.AreEqual(expected, order);
            Assert.AreEqual(3, ctx.Results.Count);
            Assert.AreEqual(pack.cardCount, dealt, "one card per result should be spawned");
            Assert.AreEqual(dealt, dealtActive, "all cards visible after CardsMoveOut");
            Assert.AreEqual(dealt, facesUp, "all cards face-up after CardsFlip");
            foreach (var cv in Object.FindFirstObjectByType<CardsRig>().Cards)
                Assert.IsFalse(cv.gameObject.activeSelf, "cards should be gone after FlyToWallet");

            // Pack rig ends in the torn state: opened body shown, full pack and flown-off strip hidden.
            var rig = Object.FindFirstObjectByType<PackRig>();
            Assert.NotNull(rig, "PackRig missing: run PackOpen > Setup Pack Rig");
            Assert.IsTrue(rig.bodyOpen.gameObject.activeSelf, "opened body should be visible");
            Assert.IsFalse(rig.full.gameObject.activeSelf, "full pack should be swapped out");
            Assert.IsFalse(rig.strip.gameObject.activeSelf, "strip should be hidden after flying off");
        }

        [UnityTest]
        public IEnumerator TapToOpen_StartsRun_AndLabelReturns()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var tap = Object.FindFirstObjectByType<TapToOpen>();
            Assert.NotNull(tap, "TapToOpen missing: run PackOpen > Clean Scene");
            var seq = tap.sequence;
            Assert.IsTrue(tap.IsIdle);
            yield return null;
            Assert.Greater(tap.label.alpha, 0.5f - 0.01f, "label visible while idle");

            tap.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            Assert.IsTrue(seq.IsRunning, "tap should start the run");
            yield return null;
            Assert.AreEqual(0f, tap.label.alpha, "label hidden during the run");

            float t = 0;
            while (seq.IsRunning && t < 25f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(seq.IsRunning);
            yield return null;
            Assert.Greater(tap.label.alpha, 0.5f, "label back after the run");
        }
    }
}
