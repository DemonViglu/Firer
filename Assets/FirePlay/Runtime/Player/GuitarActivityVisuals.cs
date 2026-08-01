using DemonViglu.FirePlay.Activity;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Optional presentation for a guitar prop. It owns only the prop and
    /// audio feedback; activity rules and Player animation requests remain in
    /// their respective layers. Attach it to the future guitar prop or Player
    /// root when the model is available.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuitarActivityVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerActivityHost _activityHost;
        [SerializeField] private Transform _guitarProp;
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("按 1-21 键对应的音效；可留空，缺失项不会报错。")]
        [SerializeField] private AudioClip[] _keyClips;

        private GuitarActivityLogic _logic;
        private int _playedKeyCount;
        private bool _loggedMissingHost;

        private void Awake()
        {
            ResolveReferences();
            if (_guitarProp != null && (_renderers == null || _renderers.Length == 0))
                _renderers = _guitarProp.GetComponentsInChildren<Renderer>(true);
            SetVisible(false);
        }

        private void OnEnable()
        {
            ResetPresentation();
        }

        private void Update()
        {
            ResolveReferences();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_activityHost == null && !_loggedMissingHost)
            {
                Debug.LogWarning("[GuitarActivityVisuals] 未找到 PlayerActivityHost，无法读取吉他活动状态。", this);
                _loggedMissingHost = true;
            }
#endif
            if (_activityHost == null)
                return;

            var session = _activityHost.ActiveSession;
            var nextLogic = session?.Logic as GuitarActivityLogic;
            if (nextLogic == null)
            {
                if (_logic != null)
                    ResetPresentation();
                return;
            }

            _logic = nextLogic;
            SetVisible(true);

            if (_logic.PlayedKeyCount > _playedKeyCount)
                PlayKeySound(_logic.LastKeyIndex);

            _playedKeyCount = _logic.PlayedKeyCount;
        }

        private void ResolveReferences()
        {
            _activityHost ??= GetComponentInParent<PlayerActivityHost>();
            _activityHost ??= PlayerActivityHost.Local;
            _activityHost ??= FindAnyObjectByType<PlayerActivityHost>();
            _audioSource ??= GetComponent<AudioSource>();
        }

        private void ResetPresentation()
        {
            _logic = null;
            _playedKeyCount = 0;
            SetVisible(false);
        }

        private void PlayKeySound(int keyIndex)
        {
            if (_audioSource == null || _keyClips == null || keyIndex < 1 || keyIndex > _keyClips.Length)
                return;

            var clip = _keyClips[keyIndex - 1];
            if (clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private void SetVisible(bool visible)
        {
            // Never disable the object that owns this presenter; a prop-root
            // setup must keep the presenter alive so it can observe the next
            // session and turn its renderers back on.
            if (_guitarProp != null
                && _guitarProp.gameObject != gameObject
                && _guitarProp.gameObject.activeSelf != visible)
                _guitarProp.gameObject.SetActive(visible);

            if (_renderers == null) return;
            foreach (var targetRenderer in _renderers)
            {
                if (targetRenderer != null)
                    targetRenderer.enabled = visible;
            }
        }
    }
}
