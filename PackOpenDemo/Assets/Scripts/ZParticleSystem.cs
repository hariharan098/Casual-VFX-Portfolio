using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace Zynga.GameEngine.UI.Effects
{

	/// <summary>
	/// Component to render a particle system in screen space.
	/// </summary>
	[ExecuteInEditMode]
	[RequireComponent(typeof(CanvasRenderer), typeof(ParticleSystem))]
	[AddComponentMenu("ZGUI/Effects/ZParticleSystem")]
	public class ZParticleSystem : MaskableGraphic
	{
		
		private Transform _transform;
		private ParticleSystem _pSystem;
		private ParticleSystem.Particle[] _particles;
		private UIVertex[] _quad = new UIVertex[4];
		private Vector4 _imageUV = Vector4.zero;
		private ParticleSystem.TextureSheetAnimationModule _textureSheetAnimation;
		private int _textureSheetAnimationFrames;
		private Vector2 _textureSheetAnimationFrameSize;
		private ParticleSystemRenderer _pRenderer;
		private Material _currentMaterial;
		private Texture _currentTexture;
		private ParticleSystem.MainModule _mainModule;

		public override Texture mainTexture
		{
			get
			{
				return _currentTexture;
			}
		}

        static readonly ProfilerMarker profileInitialize = new ProfilerMarker("ZParticle.Initialize");

		protected bool Initialize()
		{
#if ENABLE_PROFILER
            using var prof = profileInitialize.Auto();
#endif
            
			if(_transform == null)
			{
				_transform = transform;
			}

			if(_pSystem == null)
			{
				_pSystem = GetComponent<ParticleSystem>();

				if(_pSystem == null)
				{
					return false;
				}

				_mainModule = _pSystem.main;
				_pRenderer = _pSystem.GetComponent<ParticleSystemRenderer>();

				if(_pRenderer != null)
				{
					_pRenderer.enabled = false;
				}

				if(material == null)
				{
					material = new Material(Shader.Find("ZGUI/Particles/Additive"));
					material.name = "ZParticleSystemMaterial";
				}

				if(material && material.HasProperty("_MainTex"))
				{
					_currentTexture = material.mainTexture;
					if(_currentTexture == null)
					{
						_currentTexture = Texture2D.whiteTexture;
					}
				}
				_currentMaterial = material;
				_mainModule.scalingMode = ParticleSystemScalingMode.Hierarchy;
				_particles = null;
			}

			if(_particles == null)
			{
				_particles = new ParticleSystem.Particle[_pSystem.main.maxParticles];
			}

			_imageUV = new Vector4(0, 0, 1, 1);
			_textureSheetAnimation = _pSystem.textureSheetAnimation;
			_textureSheetAnimationFrames = 0;
			_textureSheetAnimationFrameSize = Vector2.zero;
			if(_textureSheetAnimation.enabled)
			{
				_textureSheetAnimationFrames = _textureSheetAnimation.numTilesX * _textureSheetAnimation.numTilesY;
				_textureSheetAnimationFrameSize = new Vector2(1f / _textureSheetAnimation.numTilesX, 1f / _textureSheetAnimation.numTilesY);
			}

			return true;
		}

		protected override void Awake()
		{
			base.Awake();
			if(!Initialize())
			{
				enabled = false;
			}
		}


        static readonly ProfilerMarker profileOnPopulateMesh = new ProfilerMarker("ZParticle.OnPopulateMesh");

		protected override void OnPopulateMesh(VertexHelper vh)
		{
#if ENABLE_PROFILER
            using var prof = profileOnPopulateMesh.Auto();
#endif
            
#if UNITY_EDITOR
			if(!Application.isPlaying)
			{
				if(!Initialize())
				{
					return;
				}
			}
#endif
			vh.Clear();

			if(!gameObject.activeInHierarchy)
			{
				return;
			}

			Vector2 temp = Vector2.zero;
			Vector2 corner1 = Vector2.zero;
			Vector2 corner2 = Vector2.zero;
			int count = _pSystem.GetParticles(_particles);

			for(int i = 0; i < count; ++i)
			{
				ParticleSystem.Particle particle = _particles[i];

				// get particle properties
				Vector2 position = (_mainModule.simulationSpace == ParticleSystemSimulationSpace.Local ? particle.position : _transform.InverseTransformPoint(particle.position));
				float rotation = -particle.rotation * Mathf.Deg2Rad;
				float rotation90 = rotation + Mathf.PI / 2;
				Color32 currentColor = particle.GetCurrentColor(_pSystem);
				float size = particle.GetCurrentSize(_pSystem) * 0.5f;

				// apply scale
				if(_mainModule.scalingMode == ParticleSystemScalingMode.Shape)
				{
					position /= canvas.scaleFactor;
				}

				// apply texture sheet animation
				Vector4 particleUV = _imageUV;
				if(_textureSheetAnimation.enabled)
				{
					float frameProgress = 1 - (particle.remainingLifetime / particle.startLifetime);

					if(_textureSheetAnimation.frameOverTime.curveMin != null)
					{
						frameProgress = _textureSheetAnimation.frameOverTime.curveMin.Evaluate(1 - (particle.remainingLifetime / particle.startLifetime));
					} else if(_textureSheetAnimation.frameOverTime.curve != null)
					{
						frameProgress = _textureSheetAnimation.frameOverTime.curve.Evaluate(1 - (particle.remainingLifetime / particle.startLifetime));
					} else if(_textureSheetAnimation.frameOverTime.constant > 0)
					{
						frameProgress = _textureSheetAnimation.frameOverTime.constant - (particle.remainingLifetime / particle.startLifetime);
					}

					frameProgress = Mathf.Repeat(frameProgress * _textureSheetAnimation.cycleCount, 1);
					int frame = 0;

					switch(_textureSheetAnimation.animation)
					{
					case ParticleSystemAnimationType.WholeSheet:
						frame = Mathf.FloorToInt(frameProgress * _textureSheetAnimationFrames);
						break;

					case ParticleSystemAnimationType.SingleRow:
						frame = Mathf.FloorToInt(frameProgress * _textureSheetAnimation.numTilesX);
						int row = _textureSheetAnimation.rowIndex;
						frame += row * _textureSheetAnimation.numTilesX;
						break;
					}

					frame %= _textureSheetAnimationFrames;
					int curColumn = (frame % _textureSheetAnimation.numTilesX);
					int curRow = (_textureSheetAnimation.numTilesY - Mathf.FloorToInt(frame / _textureSheetAnimation.numTilesX)) - 1;
					particleUV.x = curColumn * _textureSheetAnimationFrameSize.x;
					particleUV.y = curRow * _textureSheetAnimationFrameSize.y;
					particleUV.z = particleUV.x + _textureSheetAnimationFrameSize.x;
					particleUV.w = particleUV.y + _textureSheetAnimationFrameSize.y;
				}

				temp.x = particleUV.x;
				temp.y = particleUV.y;

				_quad[0] = UIVertex.simpleVert;
				_quad[0].color = currentColor;
				_quad[0].uv0 = temp;

				temp.x = particleUV.x;
				temp.y = particleUV.w;
				_quad[1] = UIVertex.simpleVert;
				_quad[1].color = currentColor;
				_quad[1].uv0 = temp;

				temp.x = particleUV.z;
				temp.y = particleUV.w;
				_quad[2] = UIVertex.simpleVert;
				_quad[2].color = currentColor;
				_quad[2].uv0 = temp;

				temp.x = particleUV.z;
				temp.y = particleUV.y;
				_quad[3] = UIVertex.simpleVert;
				_quad[3].color = currentColor;
				_quad[3].uv0 = temp;

				if(rotation == 0)
				{
					// no rotation
					corner1.x = position.x - size;
					corner1.y = position.y - size;
					corner2.x = position.x + size;
					corner2.y = position.y + size;

					temp.x = corner1.x;
					temp.y = corner1.y;
					_quad[0].position = temp;
					temp.x = corner1.x;
					temp.y = corner2.y;
					_quad[1].position = temp;
					temp.x = corner2.x;
					temp.y = corner2.y;
					_quad[2].position = temp;
					temp.x = corner2.x;
					temp.y = corner1.y;
					_quad[3].position = temp;
				} else
				{
					// apply rotation
					Vector2 right = new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * size;
					Vector2 up = new Vector2(Mathf.Cos(rotation90), Mathf.Sin(rotation90)) * size;

					_quad[0].position = position - right - up;
					_quad[1].position = position - right + up;
					_quad[2].position = position + right + up;
					_quad[3].position = position + right - up;
				}

				vh.AddUIVertexQuad(_quad);
			}
		}

        static readonly ProfilerMarker profileSetVerticesDirty = new ProfilerMarker("SetVerticesDirty");
        static readonly ProfilerMarker profileCheckReInit = new ProfilerMarker("CheckReInit");
        
		void Update()
		{
            // Optimization - skip zparticle simulation + mesh rebuilding if not visible
            if (IsMyCanvasVisible())
            {
                _pSystem.Simulate(Time.unscaledDeltaTime, false, false, true);
                profileSetVerticesDirty.Begin();
                SetVerticesDirty();
                profileSetVerticesDirty.End();
            }
            
            
            profileCheckReInit.Begin();
			if((_currentMaterial != null && _currentTexture != _currentMaterial.mainTexture) ||
			   (material != null && _currentMaterial != null && material.shader != _currentMaterial.shader))
			{
				_pSystem = null;
				Initialize();
			}
            profileCheckReInit.End();
        }
        
        /// <summary>
        /// Determines if this ZParticleSystem's canvas is visible based on it's RenderMode + Camera.
        /// Relies on camera's being disabled as appropriate, typically by dialog's setting ShouldHidePanel = true.
        /// Could do more visibility testing, like canvas viewport culling, but this is sufficient for GOTL use cases.
        /// </summary>
        /// <returns></returns>
        private bool IsMyCanvasVisible()
        {
            if (canvas == null)
            {
                return false;
            }

            // Assume screen space overlays are always visible
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return true;
            }

            // A screen space canvas with no specified camera acts like a ScreenSpaceOverlay
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                return true;
            }

            // Must be RenderMode.WorldSpace, is it's camera enabled?
            return canvas.worldCamera != null && canvas.worldCamera.isActiveAndEnabled;
        }
	}
}