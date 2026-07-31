using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// 一个活动对应一个自己的 UI Prefab。地点只引用这个资产，不复制动作槽或展示配置。
    /// </summary>
    [CreateAssetMenu(fileName = "ActivityDefinition", menuName = "FirePlay/Activity/Definition")]
    public sealed class ActivityDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string _activityId;
        [SerializeField] private string _displayName;
        [SerializeField] private ActivityScope _scope;
        [SerializeField] private ActivityParticipationMode _participationMode;
        [SerializeField] private GameObject _uiPrefab;
        [SerializeField] private string _uiPrefabKey;
        [SerializeField] private string _inputActionMap;
        [SerializeField] private string _cameraProfileId;
        [SerializeField] private string _logicKey;

        public string ActivityId => _activityId;
        public string DisplayName => _displayName;
        public ActivityScope Scope => _scope;
        public ActivityParticipationMode ParticipationMode => _participationMode;
        public GameObject UiPrefab => _uiPrefab;
        public string UiPrefabKey => _uiPrefabKey;
        public string InputActionMap => _inputActionMap;
        public string CameraProfileId => _cameraProfileId;
        public string LogicKey => _logicKey;

        public ActivityDefinition CreateRuntimeDefinition(IActivityRuleProvider defaultRules = null)
        {
            return new ActivityDefinition(
                _activityId,
                _displayName,
                _scope,
                _participationMode,
                _uiPrefabKey,
                _inputActionMap,
                _cameraProfileId,
                _logicKey,
                defaultRules);
        }

        private void OnValidate()
        {
            _activityId = _activityId?.Trim();
            _displayName = _displayName?.Trim();
            _uiPrefabKey = _uiPrefabKey?.Trim();
            _inputActionMap = _inputActionMap?.Trim();
            _cameraProfileId = _cameraProfileId?.Trim();
            _logicKey = _logicKey?.Trim();
        }
    }
}
