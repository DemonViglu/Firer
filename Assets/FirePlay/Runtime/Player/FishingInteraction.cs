using System;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 玩家侧钓鱼原型。Q 用于拟造/抛竿，E 只在上钩窗口收线。
    /// 没有背包与存档：每根竿钓满后本局结束，结果通过事件提供给未来分享或物品系统。
    /// </summary>
    public sealed class FishingInteraction : MonoBehaviour
    {
        private enum FishingState
        {
            None,
            ReadyToCast,
            WaitingForBite,
            BiteReady
        }

        [SerializeField] private RestInteraction _rest;
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private Transform _fishingRodProp;
        [SerializeField] private PlayerRitualAnimationController _ritualAnimationController;

        private FishingRitual _activeRitual;
        private FishingState _state;
        private int _catches;
        private float _stateEndsAt;

        public string Status { get; private set; } = "Sit by the water to fish";
        public bool HasRod => _state != FishingState.None;
        public bool IsLineCast => _state == FishingState.WaitingForBite || _state == FishingState.BiteReady;
        public bool IsFishBiting => _state == FishingState.BiteReady;
        public int Catches => _catches;
        public event Action RodMaterialized;
        public event Action LineCast;
        public event Action FishBit;
        public event Action<FishingResult> FishCaught;
        public event Action Cancelled;

        private void Awake()
        {
            _rest ??= GetComponent<RestInteraction>();
            _input ??= GetComponent<FirePlayPlayerInput>();
            _resourceController ??= GetComponent<FlameResourceController>();
            _ritualAnimationController ??= GetComponent<PlayerRitualAnimationController>();
            if (_rest == null || _input == null || _resourceController == null)
            {
                enabled = false;
                return;
            }

            if (_fishingRodProp != null)
            {
                _fishingRodProp.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            var ritual = _rest.IsResting && _rest.ActiveRestSpot != null
                ? _rest.ActiveRestSpot.GetComponent<FishingRitual>()
                : null;
            if (ritual != _activeRitual)
            {
                if (HasRod)
                {
                    EndSession(cancelled: true);
                }

                _activeRitual = ritual;
                Status = ritual == null ? "Sit by the water to fish" : $"Press Q: materialize fishing rod ({ritual.RodFuelCost:0})";
            }

            if (_activeRitual != null && _input.EmotePressedThisFrame)
            {
                HandleEmotePressed();
            }

            if (_activeRitual != null && _input.InteractPressedThisFrame && IsFishBiting)
            {
                ReelFish();
            }

            AdvanceTiming();
            _ritualAnimationController?.SetState(RitualAnimationState.Fishing, HasRod);
            if (_fishingRodProp != null && _fishingRodProp.gameObject.activeSelf != HasRod)
            {
                _fishingRodProp.gameObject.SetActive(HasRod);
            }
        }

        private void HandleEmotePressed()
        {
            if (!HasRod)
            {
                if (_activeRitual.TryMaterializeRod(_resourceController, out var status))
                {
                    _state = FishingState.ReadyToCast;
                    _catches = 0;
                    Status = "Press Q: cast your line";
                    RodMaterialized?.Invoke();
                }
                else
                {
                    Status = status;
                }

                return;
            }

            if (_state != FishingState.ReadyToCast)
            {
                return;
            }

            _state = FishingState.WaitingForBite;
            _stateEndsAt = Time.time + UnityEngine.Random.Range(_activeRitual.MinimumBiteDelay, _activeRitual.MaximumBiteDelay);
            Status = "The line drifts quietly...";
            _ritualAnimationController?.Play(RitualAnimationCue.FishingCast);
            LineCast?.Invoke();
        }

        private void AdvanceTiming()
        {
            if (_state == FishingState.WaitingForBite && Time.time >= _stateEndsAt)
            {
                _state = FishingState.BiteReady;
                _stateEndsAt = Time.time + _activeRitual.HookWindowSeconds;
                Status = "A fish bites! Press E to reel";
                FishBit?.Invoke();
                return;
            }

            if (_state == FishingState.BiteReady && Time.time >= _stateEndsAt)
            {
                _state = FishingState.ReadyToCast;
                Status = "It slipped away. Press Q to cast again";
            }
        }

        private void ReelFish()
        {
            _catches++;
            var result = new FishingResult(_catches, _activeRitual.CatchesPerRod, _activeRitual.FuelPerCatch);
            if (result.FuelRefund > 0f)
            {
                _resourceController.Restore(result.FuelRefund);
            }

            _ritualAnimationController?.Play(RitualAnimationCue.FishingReel);
            FishCaught?.Invoke(result);
            if (_catches >= _activeRitual.CatchesPerRod)
            {
                EndSession(cancelled: false);
                Status = "The water grows still. Your rod fades gently";
                return;
            }

            _state = FishingState.ReadyToCast;
            Status = $"Caught {_catches}/{_activeRitual.CatchesPerRod} (+{result.FuelRefund:0}). Press Q to cast again";
        }

        private void EndSession(bool cancelled)
        {
            if (cancelled && HasRod)
            {
                Cancelled?.Invoke();
            }

            _state = FishingState.None;
            _catches = 0;
            _stateEndsAt = 0f;
            _ritualAnimationController?.SetState(RitualAnimationState.Fishing, false);
        }
    }
}
