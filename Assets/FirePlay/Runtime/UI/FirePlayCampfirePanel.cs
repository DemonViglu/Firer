using DemonViglu.FirePlay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>Prefab-driven nearby campfire panel, including an estimate based on real warmth decay.</summary>
    public sealed class FirePlayCampfirePanel : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction _interaction;
        [SerializeField] private LocalPlayerContext _localPlayer;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Text _warmthText;
        [SerializeField] private Text _remainingTimeText;
        [SerializeField] private Image _warmthFill;

        private void Awake() => _panelRoot ??= gameObject;

        private void Update()
        {
            _localPlayer ??= LocalPlayerContext.Current;
            _interaction ??= _localPlayer != null ? _localPlayer.Interaction : null;
            var campfire = _interaction != null ? _interaction.NearestCampfire : null;
            var visible = campfire != null;
            if (_panelRoot.activeSelf != visible) _panelRoot.SetActive(visible);
            if (!visible) return;

            if (_warmthText != null) _warmthText.text = $"篝火暖意  {campfire.Warmth:0} / {campfire.Config.MaximumWarmth:0}";
            if (_warmthFill != null) _warmthFill.fillAmount = campfire.NormalizedWarmth;
            if (_remainingTimeText != null) _remainingTimeText.text = FormatTime(campfire.EstimatedBurnSeconds);
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "火焰暂时熄灭了";
            if (float.IsPositiveInfinity(seconds)) return "火焰会一直温暖下去";
            var duration = System.TimeSpan.FromSeconds(seconds);
            return $"还能温暖  {duration.Minutes:00}:{duration.Seconds:00}";
        }
    }
}
