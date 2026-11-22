using UnityEngine;

namespace PackOpen
{
    /// <summary>Quick IMGUI panel for testing. Toggle visibility with the backquote key.</summary>
    public class PackOpenDebugPanel : MonoBehaviour
    {
        [SerializeField] PackOpenSequence sequence;
        [SerializeField] PackData pack;

        bool _visible = false;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote)) _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible || sequence == null) return;
            float s = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            GUILayout.BeginArea(new Rect(8, 8, 190, 200), GUI.skin.box);
            GUILayout.Label("Pack Open Debug");
            GUILayout.Label("Stage: " + (sequence.CurrentStage?.ToString() ?? "idle"));
            GUI.enabled = !sequence.IsRunning;
            if (GUILayout.Button("Open Pack")) sequence.Begin(pack);
            if (GUILayout.Button("Replay last")) sequence.Replay();
            GUI.enabled = sequence.IsRunning;
            if (GUILayout.Button("Skip")) sequence.Skip();
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
