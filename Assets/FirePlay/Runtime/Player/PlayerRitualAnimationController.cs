using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public enum RitualAnimationState
    {
        Resting,
        MarshmallowRoasting,
        GuitarPlaying,
        Fishing
    }

    public enum RitualAnimationCue
    {
        Materialize,
        Turn,
        Eat,
        Cancel,
        Offer,
        GuitarBegin,
        GuitarPlay,
        FishingCast,
        FishingReel,
        ExpressionWave,
        ExpressionThanks,
        ExpressionWarmth,
        ExpressionSit
    }

    /// <summary>
    /// Player 唯一的仪式动画出口。具体仪式只表达状态和动作意图，
    /// 不直接持有 Animator 或参数字符串。
    /// </summary>
    public sealed class PlayerRitualAnimationController : MonoBehaviour
    {
        [Serializable]
        private struct BoolBinding
        {
            public RitualAnimationState state;
            public string parameterName;
        }

        [Serializable]
        private struct TriggerBinding
        {
            public RitualAnimationCue cue;
            public string parameterName;
        }

        [SerializeField] private Animator _animator;
        [SerializeField] private BoolBinding[] _boolBindings =
        {
            new() { state = RitualAnimationState.Resting, parameterName = "IsResting" },
            new() { state = RitualAnimationState.MarshmallowRoasting, parameterName = "IsMarshmallowRoasting" },
            new() { state = RitualAnimationState.GuitarPlaying, parameterName = "IsGuitarPlaying" },
            new() { state = RitualAnimationState.Fishing, parameterName = "IsFishing" }
        };
        [SerializeField] private TriggerBinding[] _triggerBindings =
        {
            new() { cue = RitualAnimationCue.Materialize, parameterName = "MarshmallowMaterialize" },
            new() { cue = RitualAnimationCue.Turn, parameterName = "MarshmallowTurn" },
            new() { cue = RitualAnimationCue.Eat, parameterName = "MarshmallowEat" },
            new() { cue = RitualAnimationCue.Cancel, parameterName = "MarshmallowCancel" },
            new() { cue = RitualAnimationCue.Offer, parameterName = "RitualOffer" },
            new() { cue = RitualAnimationCue.GuitarBegin, parameterName = "GuitarBegin" },
            new() { cue = RitualAnimationCue.GuitarPlay, parameterName = "GuitarPlay" },
            new() { cue = RitualAnimationCue.FishingCast, parameterName = "FishingCast" },
            new() { cue = RitualAnimationCue.FishingReel, parameterName = "FishingReel" },
            new() { cue = RitualAnimationCue.ExpressionWave, parameterName = "EmoteWave" },
            new() { cue = RitualAnimationCue.ExpressionThanks, parameterName = "EmoteThanks" },
            new() { cue = RitualAnimationCue.ExpressionWarmth, parameterName = "EmoteWarmth" },
            new() { cue = RitualAnimationCue.ExpressionSit, parameterName = "EmoteSit" }
        };

        private void Awake()
        {
            _animator ??= GetComponentInChildren<Animator>();
        }

        public void SetState(RitualAnimationState state, bool active)
        {
            foreach (var binding in _boolBindings)
            {
                if (binding.state == state)
                {
                    SetBool(binding.parameterName, active);
                    return;
                }
            }
        }

        public void Play(RitualAnimationCue cue)
        {
            foreach (var binding in _triggerBindings)
            {
                if (binding.cue == cue)
                {
                    SetTrigger(binding.parameterName);
                    return;
                }
            }
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
