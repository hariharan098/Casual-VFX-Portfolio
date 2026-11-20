using UnityEngine;

namespace PackOpen
{
    /// <summary>Quick IMGUI panel for testing. Toggle visibility with the backquote key.</summary>
    public class PackOpenDebugPanel : MonoBehaviour
    {
        [SerializeField] PackOpenSequence sequence;
        [SerializeField] PackData greenPack;
        [SerializeField] PackData bluePack;

        bool _forceDuplicate;
        bool _visible = true;

        public bool ConsumeForceDuplicate()
        {
            bool v = _forceDuplicate;
            _forceDuplicate = false;
            return v;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote)) _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible || sequence == null) return;
            float s = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            GUILayout.BeginArea(new Rect(8, 8, 190, 330), GUI.skin.box);
            GUILayout.Label("Pack Open Debug");
            GUILayout.Label("Stage: " + (sequence.CurrentStage?.ToString() ?? "idle"));
            GUI.enabled = !sequence.IsRunning;
            if (GUILayout.Button("Open Green (2)")) sequence.Begin(greenPack, null, ConsumeForceDuplicate());
            if (GUILayout.Button("Open Blue (3)")) sequence.Begin(bluePack, null, ConsumeForceDuplicate());
            _forceDuplicate = GUILayout.Toggle(_forceDuplicate, "Force duplicate (next)");
            if (GUILayout.Button("Replay last")) sequence.Replay();
            if (GUILayout.Button("Reset collection")) sequence.Collection.Clear();
            GUI.enabled = sequence.IsRunning;
            if (GUILayout.Button("Skip")) sequence.Skip();
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
