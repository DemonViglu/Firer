using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public static class PlayerAnimationStateIds
    {
        public const string Resting = "resting";
        public const string MarshmallowRoasting = "marshmallow.roasting";
        public const string GuitarPlaying = "guitar.playing";
        public const string Fishing = "fishing";
    }

    public static class PlayerAnimationCueIds
    {
        public const string MarshmallowMaterialize = "marshmallow.materialize";
        public const string MarshmallowTurn = "marshmallow.turn";
        public const string MarshmallowEat = "marshmallow.eat";
        public const string MarshmallowCancel = "marshmallow.cancel";
        public const string RitualOffer = "ritual.offer";
        public const string GuitarBegin = "guitar.begin";
        public const string GuitarPlay = "guitar.play";
        public const string FishingCast = "fishing.cast";
        public const string FishingReel = "fishing.reel";
    }

    /// <summary>
    /// Player 唯一的仪式动画出口。具体仪式只表达状态和动作意图，
    /// 不直接持有 Animator 或参数字符串。
    /// </summary>
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        [Serializable]
        private struct BoolBinding
        {
            public string stateId;
            public string parameterName;
        }

        [Serializable]
        private struct TriggerBinding
        {
            public string cueId;
            public string parameterName;
        }

        [SerializeField] private Animator _animator;
        [Tooltip("没有 Animator 时用于验证网络姿态的占位模型；配置正式 Animator 后自动停止修改它。")]
        [SerializeField] private Transform _placeholderVisual;
        [SerializeField, Min(1f)] private float _placeholderBlendSpeed = 10f;
        [Tooltip("吉他逐键 cue 会映射到此前缀加两位键号，例如 GuitarKey01。没有对应 Animator 参数时会安全忽略。")]
        [SerializeField] private string _guitarKeyTriggerPrefix = "GuitarKey";
        [SerializeField] private BoolBinding[] _boolBindings =
        {
            new() { stateId = PlayerAnimationStateIds.Resting, parameterName = "IsResting" },
            new() { stateId = PlayerAnimationStateIds.MarshmallowRoasting, parameterName = "IsMarshmallowRoasting" },
            new() { stateId = PlayerAnimationStateIds.GuitarPlaying, parameterName = "IsGuitarPlaying" },
            new() { stateId = PlayerAnimationStateIds.Fishing, parameterName = "IsFishing" }
        };
        [SerializeField] private TriggerBinding[] _triggerBindings =
        {
            new() { cueId = PlayerAnimationCueIds.MarshmallowMaterialize, parameterName = "MarshmallowMaterialize" },
            new() { cueId = PlayerAnimationCueIds.MarshmallowTurn, parameterName = "MarshmallowTurn" },
            new() { cueId = PlayerAnimationCueIds.MarshmallowEat, parameterName = "MarshmallowEat" },
            new() { cueId = PlayerAnimationCueIds.MarshmallowCancel, parameterName = "MarshmallowCancel" },
            new() { cueId = PlayerAnimationCueIds.RitualOffer, parameterName = "RitualOffer" },
            new() { cueId = PlayerAnimationCueIds.GuitarBegin, parameterName = "GuitarBegin" },
            new() { cueId = PlayerAnimationCueIds.GuitarPlay, parameterName = "GuitarPlay" },
            new() { cueId = PlayerAnimationCueIds.FishingCast, parameterName = "FishingCast" },
            new() { cueId = PlayerAnimationCueIds.FishingReel, parameterName = "FishingReel" },
            new() { cueId = "expression.wave", parameterName = "EmoteWave" },
            new() { cueId = "expression.thanks", parameterName = "EmoteThanks" },
            new() { cueId = "expression.warmth", parameterName = "EmoteWarmth" },
            new() { cueId = "expression.sit", parameterName = "EmoteSit" }
        };

        private readonly HashSet<string> _activeStates = new(StringComparer.Ordinal);
        private Vector3 _placeholderBasePosition;
        private Vector3 _placeholderBaseScale;
        private Quaternion _placeholderBaseRotation;
        private float _placeholderCuePulse;

        public event Action<string> CuePlayed;
        public string LastCueId { get; private set; } = string.Empty;
        public bool IsStateActive(string stateId) =>
            !string.IsNullOrWhiteSpace(stateId) && _activeStates.Contains(stateId);

        private void Awake()
        {
            _animator ??= GetComponentInChildren<Animator>();
            if (_placeholderVisual != null)
            {
                _placeholderBasePosition = _placeholderVisual.localPosition;
                _placeholderBaseScale = _placeholderVisual.localScale;
                _placeholderBaseRotation = _placeholderVisual.localRotation;
            }
        }

        private void Update()
        {
            if (_animator != null || _placeholderVisual == null)
                return;

            var targetPosition = _placeholderBasePosition;
            var targetScale = _placeholderBaseScale;
            var targetRotation = _placeholderBaseRotation;

            if (IsStateActive(PlayerAnimationStateIds.Resting))
            {
                targetPosition += Vector3.down * 0.38f;
                targetScale = Vector3.Scale(targetScale, new Vector3(1.08f, 0.62f, 1.08f));
            }
            else if (IsStateActive(PlayerAnimationStateIds.GuitarPlaying))
            {
                targetRotation *= Quaternion.Euler(0f, 0f, -8f);
            }
            else if (IsStateActive(PlayerAnimationStateIds.Fishing))
            {
                targetRotation *= Quaternion.Euler(8f, 0f, 0f);
            }
            else if (IsStateActive(PlayerAnimationStateIds.MarshmallowRoasting))
            {
                targetRotation *= Quaternion.Euler(-5f, 0f, 0f);
            }

            if (_placeholderCuePulse > 0f)
            {
                targetScale *= 1f + 0.08f * _placeholderCuePulse;
                _placeholderCuePulse = Mathf.MoveTowards(
                    _placeholderCuePulse,
                    0f,
                    Time.deltaTime * 5f);
            }

            var blend = 1f - Mathf.Exp(-_placeholderBlendSpeed * Time.deltaTime);
            _placeholderVisual.localPosition = Vector3.Lerp(
                _placeholderVisual.localPosition,
                targetPosition,
                blend);
            _placeholderVisual.localScale = Vector3.Lerp(
                _placeholderVisual.localScale,
                targetScale,
                blend);
            _placeholderVisual.localRotation = Quaternion.Slerp(
                _placeholderVisual.localRotation,
                targetRotation,
                blend);
        }

        public void SetState(string stateId, bool active)
        {
            if (string.IsNullOrWhiteSpace(stateId)) return;
            if (active) _activeStates.Add(stateId);
            else _activeStates.Remove(stateId);

            foreach (var binding in _boolBindings)
            {
                if (binding.stateId == stateId)
                {
                    SetBool(binding.parameterName, active);
                    return;
                }
            }
        }

        public void Play(string cueId)
        {
            if (string.IsNullOrWhiteSpace(cueId)) return;
            LastCueId = cueId;
            _placeholderCuePulse = 1f;
            if (!TryPlayGuitarKeyCue(cueId))
            {
                foreach (var binding in _triggerBindings)
                {
                    if (binding.cueId != cueId) continue;
                    SetTrigger(binding.parameterName);
                    break;
                }
            }

            CuePlayed?.Invoke(cueId);
        }

        private bool TryPlayGuitarKeyCue(string cueId)
        {
            const string cuePrefix = "guitar.key.";
            if (string.IsNullOrWhiteSpace(_guitarKeyTriggerPrefix)
                || !cueId.StartsWith(cuePrefix, StringComparison.Ordinal)
                || !int.TryParse(cueId.Substring(cuePrefix.Length), out var keyIndex)
                || keyIndex < 1
                || keyIndex > 21)
                return false;

            SetTrigger($"{_guitarKeyTriggerPrefix}{keyIndex:00}");
            return true;
        }

        public void ApplySharedState(PlayerSharedStateSnapshot snapshot)
        {
            // PlayerSharedState currently owns only the generic Resting mode.
            // Activity animation states have an independent Session lifecycle
            // and must not be cleared every Player tick.
            SetState(
                PlayerAnimationStateIds.Resting,
                snapshot.RitualStateId == PlayerAnimationStateIds.Resting);
        }

        private void OnDisable()
        {
            _activeStates.Clear();
            _placeholderCuePulse = 0f;
            if (_placeholderVisual == null) return;
            _placeholderVisual.localPosition = _placeholderBasePosition;
            _placeholderVisual.localScale = _placeholderBaseScale;
            _placeholderVisual.localRotation = _placeholderBaseRotation;
        }

        private void SetBool(string parameterName, bool value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
            {
                _animator.SetBool(parameterName, value);
            }
        }

        private void SetTrigger(string parameterName)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Trigger))
            {
                _animator.SetTrigger(parameterName);
            }
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType type)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            foreach (var parameter in _animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == type)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
