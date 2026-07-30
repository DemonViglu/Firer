using System;
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

        private void Awake()
        {
            _animator ??= GetComponentInChildren<Animator>();
        }

        public void SetState(string stateId, bool active)
        {
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
            foreach (var binding in _triggerBindings)
            {
                if (binding.cueId == cueId)
                {
                    SetTrigger(binding.parameterName);
                    return;
                }
            }
        }

        public void ApplySharedState(PlayerSharedStateSnapshot snapshot)
        {
            foreach (var binding in _boolBindings)
                SetBool(binding.parameterName, binding.stateId == snapshot.RitualStateId);
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
