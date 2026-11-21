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

            Assert.IsTrue(seq.Begin(pack));
            float t = 0;
            while (seq.IsRunning && t < 15f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(seq.IsRunning, "sequence did not finish on its own");

            var expected = new List<PackOpenStageId>
            {
                PackOpenStageId.PackAppear, PackOpenStageId.PackTear, PackOpenStageId.PackMoveDown,
                PackOpenStageId.CardsMoveOut, PackOpenStageId.CardsFlip, PackOpenStageId.Hold, PackOpenStageId.FlyToWallet
            };
            CollectionAssert.AreEqual(expected, order);
            Assert.AreEqual(3, ctx.Results.Count);

            // Pack rig ends in the torn state: opened body shown, full pack and flown-off strip hidden.
            var rig = Object.FindFirstObjectByType<PackRig>();
            Assert.NotNull(rig, "PackRig missing: run PackOpen > Setup Pack Rig");
            Assert.IsTrue(rig.bodyOpen.gameObject.activeSelf, "opened body should be visible");
            Assert.IsFalse(rig.full.gameObject.activeSelf, "full pack should be swapped out");
            Assert.IsFalse(rig.strip.gameObject.activeSelf, "strip should be hidden after flying off");
        }
    }
}
