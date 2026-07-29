using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// Reads the tree's accumulated contribution and selects one authored visual stage.
    /// Stage roots may contain models, lights, particles, audio, or vegetation; this component
    /// never changes contribution state or persistence.
    /// </summary>
    public sealed class WorldTreeProgressVisuals : MonoBehaviour
    {
        [SerializeField] private WorldTreeContribution _sourceTree;
        [Tooltip("Ascending contribution values that unlock stages 1, 2, ... Stage 0 is active below the first value.")]
        [SerializeField] private float[] _stageContributionThresholds = { 10f, 30f, 60f };
        [Tooltip("Visual roots for stages 0..N. Each root can hold any authored environment feedback.")]
        [SerializeField] private GameObject[] _stageRoots;

        private int _appliedStage = -1;

        public int CurrentStage => ResolveStage(_sourceTree != null ? _sourceTree.TotalContribution : 0f);
        public bool HasValidSetup => _sourceTree != null && _stageRoots != null && _stageRoots.Length > 0;

        private void Awake()
        {
            _sourceTree ??= GetComponent<WorldTreeContribution>();
            ApplyCurrentStage();
        }

        private void OnEnable()
        {
            if (_sourceTree != null)
            {
                _sourceTree.Contributed += HandleContributed;
            }
        }

        private void OnDisable()
        {
            if (_sourceTree != null)
            {
                _sourceTree.Contributed -= HandleContributed;
            }
        }

        private void Update()
        {
            // Loading applies saved tree state after Awake. Polling only the integer stage keeps
            // this visual bridge independent from the save system and future async repositories.
            var stage = CurrentStage;
            if (stage != _appliedStage)
            {
                ApplyStage(stage);
            }
        }

        [ContextMenu("Apply Current Stage")]
        public void ApplyCurrentStage()
        {
            ApplyStage(CurrentStage);
        }

        private void HandleContributed(WorldTreeContribution tree, Color color)
        {
            ApplyCurrentStage();
        }

        private int ResolveStage(float totalContribution)
        {
            if (_stageRoots == null || _stageRoots.Length == 0)
            {
                return 0;
            }

            var stage = 0;
            if (_stageContributionThresholds != null)
            {
                var thresholdCount = Mathf.Min(_stageContributionThresholds.Length, _stageRoots.Length - 1);
                for (var index = 0; index < thresholdCount; index++)
                {
                    if (totalContribution >= _stageContributionThresholds[index])
                    {
                        stage = index + 1;
                    }
                }
            }

            return stage;
        }

        private void ApplyStage(int stage)
        {
            if (_stageRoots == null || _stageRoots.Length == 0)
            {
                _appliedStage = stage;
                return;
            }

            var activeIndex = Mathf.Clamp(stage, 0, _stageRoots.Length - 1);
            for (var index = 0; index < _stageRoots.Length; index++)
            {
                var stageRoot = _stageRoots[index];
                if (stageRoot != null && stageRoot.activeSelf != (index == activeIndex))
                {
                    stageRoot.SetActive(index == activeIndex);
                }
            }

            _appliedStage = activeIndex;
        }

        private void OnValidate()
        {
            if (_stageContributionThresholds == null)
            {
                return;
            }

            for (var index = 0; index < _stageContributionThresholds.Length; index++)
            {
                var minimum = index == 0 ? 0f : _stageContributionThresholds[index - 1];
                _stageContributionThresholds[index] = Mathf.Max(minimum, _stageContributionThresholds[index]);
            }
        }
    }
}
