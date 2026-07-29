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

        public string Status { get; private set; } = "坐到湖边，静静钓一会儿吧";
        public bool HasRod => _state != FishingState.None;
        public bool IsLineCast => _state == FishingState.WaitingForBite || _state == FishingState.BiteReady;
        public bool IsFishBiting => _state == FishingState.BiteReady;
        public int Catches => _catches;
        public event Action RodMaterialized;
        public event Action LineCast;
        public event Action FishBit;
        public event Action<FishingResult> FishCaught;
        public event Action Cancelled;

        /// <summary>
        /// Dedicated mobile entry point for the primary fishing button. Keeping
        /// this direct avoids relying on a virtual input press surviving until a
        /// later Update tick on Android; cost validation remains in FishingRitual.
        /// </summary>
        public bool TryPrimaryAction()
        {
            if (_activeRitual == null)
            {
                return false;
            }

            HandleEmotePressed();
            return true;
        }

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
                Status = ritual == null ? "坐到湖边，静静钓一会儿吧" : $"消耗 {ritual.RodFuelCost:0} 点余火，拟造一根鱼竿";
            }

            if (_activeRitual != null && _input.EmotePressedThisFrame)
            {
                TryPrimaryAction();
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
                    Status = "鱼竿准备好了，把鱼线轻轻抛进水里吧";
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
            Status = "鱼线随着水波轻轻漂着……";
            _ritualAnimationController?.Play(RitualAnimationCue.FishingCast);
            LineCast?.Invoke();
        }

        private void AdvanceTiming()
        {
            if (_state == FishingState.WaitingForBite && Time.time >= _stateEndsAt)
            {
                _state = FishingState.BiteReady;
                _stateEndsAt = Time.time + _activeRitual.HookWindowSeconds;
                Status = "有鱼儿上钩了，快轻轻收线";
                FishBit?.Invoke();
                return;
            }

            if (_state == FishingState.BiteReady && Time.time >= _stateEndsAt)
            {
                _state = FishingState.ReadyToCast;
                Status = "鱼儿溜走了，再试一次吧";
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
                Status = "湖面又安静下来，鱼竿化作一点暖光";
                return;
            }

            _state = FishingState.ReadyToCast;
            Status = $"钓到第 {_catches}/{_activeRitual.CatchesPerRod} 条鱼，回收 {result.FuelRefund:0} 点余火";
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
