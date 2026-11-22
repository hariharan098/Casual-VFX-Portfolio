using System.Collections;
using UnityEngine;

namespace PackOpen
{
    /// <summary>
    /// Drives a UI particle prefab (ZParticleSystem children) that lives under the Canvas.
    /// Works through the standard ParticleSystem components, so it needs no reference to ZParticleSystem.
    /// Add it to the scene instance of the VFX prefab. Follow() tracks a target's position only (no scale/rotation).
    /// </summary>
    public class UiVfx : MonoBehaviour
    {
        [Tooltip("Multiplies the prefab's authored scale. Use it to size the VFX against the pack.")]
        public float scaleMultiplier = 1f;

        Transform _target;
        Vector2 _offset;
        Vector3 _baseScale = Vector3.one;
        bool _init;
        ParticleSystem[] _systems;
        CanvasGroup _group;
        float _lifetime;

        void EnsureInit()
        {
            if (_init) return;
            _init = true;
            _baseScale = transform.localScale;
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in _systems)
            {
                var m = ps.main;
                _lifetime = Mathf.Max(_lifetime, m.startLifetime.constantMax);
            }
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        public bool IsActive => gameObject.activeSelf;

        public void Follow(Transform target, Vector2 canvasOffset)
        {
            _target = target;
            _offset = canvasOffset;
            ApplyFollow();
        }

        /// <summary>Shows the VFX and restarts all its particle systems.</summary>
        public void Play()
        {
            EnsureInit();
            StopAllCoroutines();
            gameObject.SetActive(true);
            SetAlpha(1f);
            SetPop(1f);
            foreach (var ps in _systems)
            {
                var e = ps.emission; e.enabled = true;
                ps.Clear(true);
                ps.Play(true);
            }
            ApplyFollow();
        }

        /// <summary>Stops emitting; existing particles finish, then the object turns off.</summary>
        public void Stop()
        {
            EnsureInit();
            if (!gameObject.activeSelf) return;
            foreach (var ps in _systems) { var e = ps.emission; e.enabled = false; }
            StopAllCoroutines();
            StartCoroutine(DisableAfter(_lifetime + 0.1f));
        }

        /// <summary>Turns it off immediately (skip / replay).</summary>
        public void Hide()
        {
            EnsureInit();
            StopAllCoroutines();
            foreach (var ps in _systems) ps.Clear(true);
            _target = null;
            gameObject.SetActive(false);
        }

        /// <summary>Alpha of the whole effect (0..1).</summary>
        public void SetAlpha(float a) { EnsureInit(); _group.alpha = a; }

        /// <summary>Scale relative to the authored scale (times scaleMultiplier). 1 = normal.</summary>
        public void SetPop(float k) { EnsureInit(); transform.localScale = _baseScale * (scaleMultiplier * k); }

        IEnumerator DisableAfter(float t)
        {
            yield return new WaitForSecondsRealtime(t);
            _target = null;
            gameObject.SetActive(false);
        }

        void LateUpdate() { ApplyFollow(); }

        void ApplyFollow()
        {
            if (_target == null) return;
            var parent = transform.parent;
            Vector3 world = parent != null ? parent.TransformVector(new Vector3(_offset.x, _offset.y, 0f)) : (Vector3)_offset;
            transform.position = _target.position + world;
        }
    }
}
