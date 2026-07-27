using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Save;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 大树的本局贡献状态入口。
    /// 仅管理余火消耗与累计值；光点、树冠表现、存档与最终解锁条件由独立模块读取该状态。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class WorldTreeContribution : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _contributionCost = 10f;
        [SerializeField, Min(0f)] private float _totalContribution;
        [SerializeField, Min(0)] private int _contributionCount;
        [SerializeField] private Color _selectedLightColor = Color.white;
        [SerializeField] private Light _personalLight;
        [SerializeField] private bool _hasLocalContribution;
        [SerializeField] private TreePersonalLightVisuals _personalLightVisuals;
        private readonly List<TreePersonalLightRecord> _personalLights = new();

        public float ContributionCost => _contributionCost;
        public float TotalContribution => _totalContribution;
        public int ContributionCount => _contributionCount;
        public Color SelectedLightColor => _selectedLightColor;
        public bool HasLocalContribution => _hasLocalContribution;
        public IReadOnlyList<TreePersonalLightRecord> PersonalLights => _personalLights;
        public string LastContributionStatus { get; private set; } = "Ready";

        public event Action<WorldTreeContribution, Color> Contributed;

        private void Awake()
        {
            ApplyPersonalLightVisuals();
        }

        public void SelectPersonalLightColor(Color color)
        {
            if (_hasLocalContribution)
            {
                return;
            }

            _selectedLightColor = color;
        }

        public bool TryContribute(FlameResourceController resourceController, FlameBrush flame)
        {
            if (resourceController == null || resourceController.State == null || flame == null)
            {
                LastContributionStatus = "Missing setup";
                return false;
            }

            if (_hasLocalContribution)
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
            _hasLocalContribution = true;
            _personalLights.RemoveAll(record => record.playerId == "local.player");
            _personalLights.Add(new TreePersonalLightRecord { playerId = "local.player", color = _selectedLightColor, positionSeed = 0.37f });
            LastContributionStatus = "Contributed";
            ApplyPersonalLightVisuals();
            Contributed?.Invoke(this, _selectedLightColor);
            return true;
        }

        public TreeProgressData CreateRecord() => new()
        {
            hasContributed = _hasLocalContribution,
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
                _personalLights.Add(new TreePersonalLightRecord { playerId = "local.player", color = _selectedLightColor, positionSeed = 0.37f });
            }
            LastContributionStatus = _hasLocalContribution ? "Restored" : "Ready";
            ApplyPersonalLightVisuals();
        }

        private void ApplyPersonalLightVisuals()
        {
            if (_personalLight != null)
            {
                _personalLight.enabled = _hasLocalContribution;
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
    }
}
