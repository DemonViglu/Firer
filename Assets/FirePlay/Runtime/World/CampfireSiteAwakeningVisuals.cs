using DemonViglu.FirePlay.Core;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 将一处已作者布置的地点表现绑定到指定公共篝火的持久化成长等级。
    /// 它只切换美术根节点；不读取当前热量、不修改篝火状态，也不参与存档。
    /// </summary>
    public sealed class CampfireSiteAwakeningVisuals : MonoBehaviour
    {
        [SerializeField] private Campfire _sourceCampfire;
        [Tooltip("可选：作者预置的 SmallFire 的 StableSceneId。它升级为运行时 Campfire 后会自动被识别，读档后也有效。")]
        [SerializeField] private StableSceneId _sourceSmallFireSite;
        [Tooltip("阶段 0..3 的场景根物体。每个根可包含灯光、粒子、音频、植物和模型。")]
        [SerializeField] private GameObject[] _stageRoots;

        private int _appliedStage = -1;
        private string _sourceSmallFireId;

        public int CurrentStage
        {
            get
            {
                var campfire = ResolveSourceCampfire();
                if (campfire != null)
                {
                    return campfire.Level;
                }

                return Campfire.TryGetRetiredSourceStage(_sourceSmallFireId, out var retiredStage)
                    ? retiredStage
                    : 0;
            }
        }

        public bool HasValidSetup => (_sourceCampfire != null || _sourceSmallFireSite != null) &&
                                     _stageRoots != null && _stageRoots.Length > 0;

        private void Awake()
        {
            // The authored SmallFire is destroyed when it becomes a runtime Campfire.
            // Keep its stable value rather than relying on the soon-to-be-destroyed component reference.
            _sourceSmallFireId = _sourceSmallFireSite != null ? _sourceSmallFireSite.Value : null;
            ApplyCurrentStage();
        }

        private void Update()
        {
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

            _appliedStage = stage;
        }

        private Campfire ResolveSourceCampfire()
        {
            if (_sourceCampfire != null)
            {
                return _sourceCampfire;
            }

            if (string.IsNullOrWhiteSpace(_sourceSmallFireId))
            {
                return null;
            }

            foreach (var campfire in Campfire.ActiveInstances)
            {
                if (campfire != null && campfire.SourceSmallFireId == _sourceSmallFireId)
                {
                    return campfire;
                }
            }

            return null;
        }
    }
}
