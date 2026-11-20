using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
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

        IEnumerator Open(string packId, bool forceDup, List<PackOpenStageId> order, PackOpenContext[] ctxOut)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var seq = Object.FindFirstObjectByType<PackOpenSequence>();
            Assert.NotNull(seq);
            var pack = AssetDatabase.LoadAssetAtPath<PackData>($"Assets/Data/Packs/Pack_{packId}.asset");
            seq.StageStarted += (id, ctx) => { order.Add(id); ctxOut[0] = ctx; };

            Assert.IsTrue(seq.Begin(pack, null, forceDup));
            float t = 0;
            while (seq.CurrentStage != PackOpenStageId.TapToClaim && t < 10f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(PackOpenStageId.TapToClaim, seq.CurrentStage, "never reached TapToClaim");
            seq.Skip(); // stands in for the player's tap
            t = 0;
            while (seq.IsRunning && t < 10f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(seq.IsRunning, "sequence did not finish");
        }

        [UnityTest]
        public IEnumerator GreenPack_RunsAllStages_NoDuplicateStage()
        {
            var order = new List<PackOpenStageId>(); var ctx = new PackOpenContext[1];
            yield return Open("green", false, order, ctx);
            Assert.AreEqual(2, ctx[0].Results.Count);
            Assert.AreEqual(PackVariant.Burst, ctx[0].Variant);
            Assert.IsFalse(order.Contains(PackOpenStageId.DuplicateStars));
            Assert.AreEqual(PackOpenStageId.FlyToTab, order[order.Count - 1]);
        }

        [UnityTest]
        public IEnumerator BluePack_ForcedDuplicate_RunsDuplicateStars()
        {
            var order = new List<PackOpenStageId>(); var ctx = new PackOpenContext[1];
            yield return Open("blue", true, order, ctx);
            Assert.AreEqual(3, ctx[0].Results.Count);
            Assert.AreEqual(PackVariant.TearDrop, ctx[0].Variant);
            Assert.IsFalse(ctx[0].Results[0].IsNew);
            Assert.AreEqual(ctx[0].Results[0].Card.rarity, ctx[0].Results[0].StarsFromDuplicate);
            Assert.AreEqual(PackOpenStageId.DuplicateStars, order[order.Count - 1]);
        }
    }
}
