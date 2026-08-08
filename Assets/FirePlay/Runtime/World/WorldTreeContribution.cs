using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Save;
using DemonViglu.FirePlay.Core;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 大树的本局贡献状态入口。
    /// 仅管理余火消耗与累计值；光点、树冠表现、存档与最终解锁条件由独立模块读取该状态。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(StableSceneId))]
    public sealed class WorldTreeContribution : MonoBehaviour, IWorldCommandVersioned
    {
        [SerializeField, Min(0.1f)] private float _contributionCost = 10f;
        [SerializeField, Min(0f)] private float _totalContribution;
        [SerializeField, Min(0)] private int _contributionCount;
        [SerializeField] private Color _selectedLightColor = Color.white;
        [SerializeField] private Light _personalLight;
        [SerializeField] private bool _hasLocalContribution;
        [SerializeField] private TreePersonalLightVisuals _personalLightVisuals;
        private readonly List<TreePersonalLightRecord> _personalLights = new();
        private bool _simulateAuthority = true;

        public float ContributionCost => _contributionCost;
        public float TotalContribution => _totalContribution;
        public int ContributionCount => _contributionCount;
        public Color SelectedLightColor => _selectedLightColor;
        public bool HasLocalContribution => HasContribution(ResolveLocalPlayerId());
        public IReadOnlyList<TreePersonalLightRecord> PersonalLights => _personalLights;
        public string LastContributionStatus { get; private set; } = "Ready";
        public uint CommandVersion { get; private set; }

        public event Action<WorldTreeContribution, Color> Contributed;

        private void Awake()
        {
            var stableId = GetComponent<StableSceneId>();
            if (stableId == null || !stableId.IsValid)
            {
                Debug.LogError("[WorldTreeContribution] 需要显式配置 StableSceneId。", this);
            }
            ApplyPersonalLightVisuals();
        }

        public void SelectPersonalLightColor(Color color)
        {
            if (HasLocalContribution)
            {
                return;
            }

            _selectedLightColor = color;
        }

        public bool TryContribute(string actorId, FlameResourceController resourceController, FlameBrush flame)
        {
            return TryContribute(actorId, resourceController, flame, _selectedLightColor);
        }

        public bool TryContribute(
            string actorId,
            FlameResourceController resourceController,
            FlameBrush flame,
            Color selectedLightColor)
        {
            if (!_simulateAuthority)
            {
                LastContributionStatus = "Host authority required";
                return false;
            }
            if (string.IsNullOrWhiteSpace(actorId) || resourceController == null || resourceController.State == null || flame == null)
            {
                LastContributionStatus = "Missing setup";
                return false;
            }

            if (HasContribution(actorId))
            {
                LastContributionStatus = "Already contributed";
                return false;
            }

            if (resourceController.State.CurrentFuel < _contributionCost)
            {
                LastContributionStatus = "Not enough fuel";
                return false;
            }

            if (!resourceController.TryConsume(_contributionCost))
            {
                LastContributionStatus = "Consume failed";
                return false;
            }

            _totalContribution += _contributionCost;
            _contributionCount++;
            _selectedLightColor = ClampColor(selectedLightColor);
            _personalLights.RemoveAll(record => record.playerId == actorId);
            _personalLights.Add(new TreePersonalLightRecord { playerId = actorId, color = _selectedLightColor, positionSeed = CreatePositionSeed(actorId) });
            _hasLocalContribution = HasLocalContribution;
            LastContributionStatus = "Contributed";
            CommandVersion++;
            ApplyPersonalLightVisuals();
            Contributed?.Invoke(this, _selectedLightColor);
            return true;
        }

        public void ConfigureSimulation(bool simulateAuthority)
        {
            _simulateAuthority = simulateAuthority;
        }

        public void ApplyNetworkSnapshot(
            float totalContribution,
            int contributionCount,
            IReadOnlyList<TreePersonalLightRecord> personalLights,
            uint commandVersion)
        {
            _totalContribution = Mathf.Max(0f, totalContribution);
            _contributionCount = Mathf.Max(0, contributionCount);
            CommandVersion = commandVersion;
            _personalLights.Clear();
            if (personalLights != null)
            {
                foreach (var record in personalLights)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.playerId))
                        continue;
                    _personalLights.Add(new TreePersonalLightRecord
                    {
                        playerId = record.playerId,
                        color = ClampColor(record.color),
                        positionSeed = Mathf.Clamp01(record.positionSeed)
                    });
                }
            }

            var localPlayerId = ResolveLocalPlayerId();
            var localRecord = _personalLights.Find(record => record.playerId == localPlayerId);
            _hasLocalContribution = localRecord != null;
            if (localRecord != null)
                _selectedLightColor = localRecord.color;
            LastContributionStatus = _hasLocalContribution ? "Contributed" : "Synchronized";
            ApplyPersonalLightVisuals();
        }

        public TreeProgressData CreateRecord() => new()
        {
            hasContributed = HasLocalContribution,
            totalContribution = _totalContribution,
            contributionCount = _contributionCount,
            personalLightColor = _selectedLightColor,
            personalLights = new List<TreePersonalLightRecord>(_personalLights)
        };

        public void ApplySavedState(TreeProgressData data)
        {
            if (data == null)
            {
                return;
            }

            _hasLocalContribution = data.hasContributed;
            _totalContribution = Mathf.Max(0f, data.totalContribution);
            _contributionCount = Mathf.Max(0, data.contributionCount);
            _selectedLightColor = data.personalLightColor;
            _personalLights.Clear();
            if (data.personalLights != null)
            {
                _personalLights.AddRange(data.personalLights);
            }
            if (_hasLocalContribution && _personalLights.Count == 0)
            {
                var localPlayerId = ResolveLocalPlayerId();
                _personalLights.Add(new TreePersonalLightRecord { playerId = localPlayerId, color = _selectedLightColor, positionSeed = CreatePositionSeed(localPlayerId) });
            }
            _hasLocalContribution = HasLocalContribution;
            LastContributionStatus = _hasLocalContribution ? "Restored" : "Ready";
            CommandVersion++;
            ApplyPersonalLightVisuals();
        }

        private void ApplyPersonalLightVisuals()
        {
            if (_personalLight != null)
            {
                _personalLight.enabled = HasLocalContribution;
                _personalLight.color = _selectedLightColor;
            }

            _personalLightVisuals?.Apply(_personalLights);
        }

        private void OnValidate()
        {
            _contributionCost = Mathf.Max(0.1f, _contributionCost);
            _totalContribution = Mathf.Max(0f, _totalContribution);
            _contributionCount = Mathf.Max(0, _contributionCount);
        }

        private bool HasContribution(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return false;
            return _personalLights.Exists(record => record != null && record.playerId == playerId)
                || (_hasLocalContribution && _personalLights.Count == 0 && playerId == ResolveLocalPlayerId());
        }

        private static string ResolveLocalPlayerId()
        {
            return GameInstanceSubsystem.TryGet<DemonViglu.FirePlay.Player.IPlayerIdentityService>()?.LocalPlayerId
                ?? "local.player";
        }

        private static float CreatePositionSeed(string playerId)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var character in playerId)
                {
                    hash ^= character;
                    hash *= 16777619;
                }
                return (hash & 0x00FFFFFF) / 16777215f;
            }
        }

        private static Color ClampColor(Color color) => new(
            Mathf.Clamp01(color.r),
            Mathf.Clamp01(color.g),
            Mathf.Clamp01(color.b),
            Mathf.Clamp01(color.a));
    }
}
