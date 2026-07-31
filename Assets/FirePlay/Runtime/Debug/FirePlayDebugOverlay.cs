using System.Text;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemonViglu.FirePlay.Debugging
{
    /// <summary>
    /// 原型阶段的零依赖调试面板。尚未实现的里程碑数据会明确显示为未接入，
    /// 避免使用临时假数据掩盖系统状态。
    /// </summary>
    public sealed class FirePlayDebugOverlay : MonoBehaviour
    {
        //[SerializeField] private PlayerFlameController _playerFlameController;
        //[SerializeField] private PlayerMovement _playerMovement;
        //[SerializeField] private PlayerInteraction _playerInteraction;
        //[SerializeField] private CampfirePlacement _campfirePlacement;
        //[SerializeField] private FlameResourceController _flameResourceController;
        //[SerializeField] private RestInteraction _restInteraction;
        //[SerializeField] private bool _visible = true;
        //[SerializeField] private Vector2 _screenPosition = new(16f, 16f);
        //[SerializeField, Min(240f)] private float _width = 360f;

        //private readonly StringBuilder _text = new(256);
        //private GUIStyle _labelStyle;
        //private float _smoothedDeltaTime;
        //private float _nextMissingReferenceSearchTime;

        //public string FlameResourceStatus { get; private set; } = "未接入（M1）";
        //public string FlameModeStatus { get; private set; } = "未接入（M1）";
        //public string NearbyCampfireStatus { get; private set; } = "未接入（M2/M3）";
        //public string SaveStatus { get; private set; } = "未接入（M3）";

        //public void SetVisible(bool visible) => _visible = visible;

        //public void SetFlameStatus(string resourceStatus, string modeStatus)
        //{
        //    FlameResourceStatus = NormalizeStatus(resourceStatus);
        //    FlameModeStatus = NormalizeStatus(modeStatus);
        //}

        //public void SetNearbyCampfireStatus(string status) => NearbyCampfireStatus = NormalizeStatus(status);
        //public void SetSaveStatus(string status) => SaveStatus = NormalizeStatus(status);

        //private void Awake()
        //{
        //    ResolveReferences();
        //}

        //private void ResolveReferences()
        //{
        //    if (_playerFlameController == null)
        //    {
        //        _playerFlameController = FindFirstObjectByType<PlayerFlameController>();
        //    }

        //    if (_playerMovement == null)
        //    {
        //        _playerMovement = FindFirstObjectByType<PlayerMovement>();
        //    }

        //    if (_playerInteraction == null)
        //    {
        //        _playerInteraction = FindFirstObjectByType<PlayerInteraction>();
        //    }

        //    if (_campfirePlacement == null)
        //    {
        //        _campfirePlacement = FindFirstObjectByType<CampfirePlacement>();
        //    }

        //    if (_flameResourceController == null)
        //    {
        //        _flameResourceController = FindFirstObjectByType<FlameResourceController>();
        //    }

        //    if (_restInteraction == null)
        //    {
        //        _restInteraction = FindFirstObjectByType<RestInteraction>();
        //    }

        //    if (_marshmallowInteraction == null)
        //    {
        //    }

        //    if (_fishingInteraction == null)
        //    {
        //    }
        //}

        //private void Update()
        //{
        //    _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime, Time.unscaledDeltaTime, 0.1f);
        //    if (Time.unscaledTime >= _nextMissingReferenceSearchTime)
        //    {
        //        ResolveReferences();
        //        _nextMissingReferenceSearchTime = Time.unscaledTime + 1f;
        //    }
        //}

        //private void OnGUI()
        //{
        //    if (!_visible)
        //    {
        //        return;
        //    }

        //    EnsureStyle();
        //    BuildText();

        //    var content = new GUIContent(_text.ToString());
        //    var height = _labelStyle.CalcHeight(content, _width);
        //    GUI.Label(new Rect(_screenPosition.x, _screenPosition.y, _width, height), content, _labelStyle);
        //}

        //private void BuildText()
        //{
        //    var activeFlame = _playerFlameController != null ? _playerFlameController.ActiveFlame : null;
        //    var fps = _smoothedDeltaTime > 0.0001f ? 1f / _smoothedDeltaTime : 0f;

        //    _text.Clear();
        //    _text.AppendLine("FirePlay / Runtime Debug");
        //    _text.Append("Scene: ").AppendLine(SceneManager.GetActiveScene().name);
        //    _text.Append("FPS: ").AppendLine(fps.ToString("0"));
        //    _text.Append("Player: ").AppendLine(_playerFlameController != null ? "OK" : "MISSING");
        //    _text.Append("Movement: ").AppendLine(_playerMovement == null
        //        ? "MISSING COMPONENT"
        //        : _playerMovement.MovementLocked ? "Locked" : _playerMovement.IsSprinting ? "Sprinting" : "Walking");
        //    _text.Append("Active Flame: ").AppendLine(activeFlame != null ? activeFlame.name : "MISSING");

        //    if (activeFlame != null)
        //    {
        //        _text.Append("Flame Intensity: ").AppendLine(activeFlame.State.Intensity.ToString("0.00"));
        //    }

        //    var resourceState = _flameResourceController != null ? _flameResourceController.State : null;
        //    if (resourceState != null)
        //    {
        //        _text.Append("Fuel: ")
        //            .Append(resourceState.CurrentFuel.ToString("0.0"))
        //            .Append(" / ")
        //            .AppendLine(resourceState.MaxFuel.ToString("0.0"));
        //        _text.Append("Mode: ").AppendLine(resourceState.Mode.ToString());
        //        _text.Append("Contraction: ").AppendLine(
        //            resourceState.IsReceiverOverride
        //                ? "Active"
        //                : _flameResourceController.IsInSafeZone ? "Available (Hold C)" : "Unsafe");
        //        _text.Append("Resource Flow: ").AppendLine(
        //            _flameResourceController.IsRecovering
        //                ? "Recovering"
        //                : _flameResourceController.CurrentCampfireDrainMultiplier < 0.999f
        //                    ? $"Campfire comfort x{_flameResourceController.CurrentCampfireDrainMultiplier:0.00}"
        //                : _flameResourceController.NightDrainActive ? "Night Drain" : "Paused");
        //    }
        //    else
        //    {
        //        _text.Append("Fuel: ").AppendLine(FlameResourceStatus);
        //        _text.Append("Mode: ").AppendLine(FlameModeStatus);
        //    }
        //    var nearbyFlameSource = _playerInteraction != null ? _playerInteraction.NearestFlameSource : null;
        //    var nearbySmallFire = _playerInteraction != null ? _playerInteraction.NearestSmallFire : null;
        //    var nearbyCampfire = _playerInteraction != null ? _playerInteraction.NearestCampfire : null;
        //    var nearbyWorldTree = _playerInteraction != null ? _playerInteraction.NearestWorldTree : null;
        //    _text.Append("Interaction: ").AppendLine(_playerInteraction == null
        //        ? "MISSING COMPONENT"
        //        : _playerInteraction.CurrentInteractPrompt);
        //    if (nearbyCampfire != null)
        //    {
        //        _text.Append("Nearby Campfire: ").Append(nearbyCampfire.CampfireId).Append(" / Level ")
        //            .Append(nearbyCampfire.Level).Append(" / Warmth ")
        //            .AppendLine($"{nearbyCampfire.Warmth:0.0} / 100");
        //        _text.Append("Campfire Status: ").AppendLine(nearbyCampfire.LastUpgradeStatus);
        //    }
        //    else if (nearbySmallFire != null)
        //    {
        //        var upgradeController = _playerInteraction.CampfireUpgradeController;
        //        _text.Append("Nearby Fire: Small fire").AppendLine();
        //        _text.Append("Small Fire Growth: ").AppendLine(upgradeController == null
        //            ? "MISSING COMPONENT"
        //            : upgradeController.HasValidSetup ? "Press E" : "INVALID SETUP");
        //        if (upgradeController != null)
        //        {
        //            _text.Append("Upgrade Status: ").AppendLine(upgradeController.LastUpgradeStatus);
        //        }
        //    }
        //    else if (nearbyFlameSource != null)
        //    {
        //        _text.Append("Nearby Fire: ").AppendLine(nearbyFlameSource.SourceId);
        //    }
        //    else
        //    {
        //        _text.Append("Nearby Fire: ").AppendLine(NearbyCampfireStatus);
        //    }
        //    _text.Append("World Tree: ").AppendLine(nearbyWorldTree == null
        //        ? "Unavailable"
        //        : nearbyWorldTree.HasLocalContribution
        //            ? $"Contributed / Total {nearbyWorldTree.TotalContribution:0.0} / {nearbyWorldTree.LastContributionStatus}"
        //            : $"Available / Total {nearbyWorldTree.TotalContribution:0.0} / {nearbyWorldTree.LastContributionStatus}");
        //    _text.Append("Save: ").AppendLine(SaveStatus);
        //    _text.Append("Small Fires: ").Append(SmallFire.ActiveCount).Append(" / ");
        //    _text.AppendLine(_campfirePlacement != null
        //        ? _campfirePlacement.MaximumActiveFireCount.ToString()
        //        : "MISSING PLACEMENT COMPONENT");
        //    if (_campfirePlacement == null)
        //    {
        //        _text.AppendLine("Placement: MISSING COMPONENT");
        //    }
        //    else
        //    {
        //        _text.Append("Placement: ").AppendLine(_campfirePlacement.PlacementStatus);
        //        if (!_campfirePlacement.HasRequiredSetup)
        //        {
        //            _text.AppendLine("Placement Setup: INVALID");
        //        }
        //    }

        //    _text.Append("Rest: ").AppendLine(_restInteraction == null
        //        ? "MISSING COMPONENT"
        //        : _restInteraction.IsResting ? $"Resting at {_restInteraction.ActiveRestSpot.name}" : _restInteraction.NearestRestSpot != null ? "Available (Press R)" : "Unavailable");
        //    _text.Append("Marshmallow: ").AppendLine(_marshmallowInteraction == null
        //        ? "MISSING COMPONENT"
        //        : _marshmallowInteraction.Status);
        //    _text.Append("Fishing: ").AppendLine(_fishingInteraction == null
        //        ? "MISSING COMPONENT"
        //        : _fishingInteraction.Status);
        //}

        //private void EnsureStyle()
        //{
        //    if (_labelStyle != null)
        //    {
        //        return;
        //    }

        //    _labelStyle = new GUIStyle(GUI.skin.box)
        //    {
        //        alignment = TextAnchor.UpperLeft,
        //        fontSize = 14,
        //        padding = new RectOffset(10, 10, 8, 8),
        //        wordWrap = true
        //    };
        //}

        //private static string NormalizeStatus(string status)
        //{
        //    return string.IsNullOrWhiteSpace(status) ? "未知" : status.Trim();
        //}
    }
}
