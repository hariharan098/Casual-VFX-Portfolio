using UnityEngine;
using UnityEditor;

namespace Zynga.GameEngine.UI.Effects.Editor
{

	[CustomEditor(typeof(ZParticleSystem))]
	public class ZParticleSystemEditor : UnityEditor.Editor
	{

		private SerializedProperty _material;
		private SerializedProperty _raycastTarget;
		private bool _usingCorrectShader;

		private void OnEnable()
		{
			_material = serializedObject.FindProperty("m_Material");
			_raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
		}

		public override void OnInspectorGUI()
		{
			var mat = ((Material)_material.objectReferenceValue);
			if(mat == null)
			{
				_material.objectReferenceValue = new Material(Shader.Find("ZGUI/Particles/Additive"));
				_material.objectReferenceValue.name = "ZParticleSystemMaterial";
				_usingCorrectShader = true;
			} else
			{
				_usingCorrectShader = mat.shader.name.Contains("ZGUI/Particles/");
			}

			if(!_usingCorrectShader)
			{
				EditorGUILayout.HelpBox("Material must use a ZGUI/Particles shader to render with UI", MessageType.Warning);
			}
			EditorGUILayout.PropertyField(_material);
			EditorGUILayout.PropertyField(_raycastTarget);
			serializedObject.ApplyModifiedProperties();
		}
	}
}

