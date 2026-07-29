using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>Prefab-driven ritual panel state. It never creates or lays out UI.</summary>
    public sealed class FirePlayRitualPanel : MonoBehaviour
    {
        [SerializeField] private RestInteraction _rest;
        [SerializeField] private MarshmallowInteraction _marshmallow;
        [SerializeField] private FishingInteraction _fishing;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private GameObject _marshmallowContent;
        [SerializeField] private GameObject _fishingContent;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _primaryActionText;
        [SerializeField] private Text _secondaryActionText;

        private void Awake()
        {
            _panelRoot ??= gameObject;
        }

        private void Update()
        {
            _rest ??= FindAnyObjectByType<RestInteraction>();
            _marshmallow ??= FindAnyObjectByType<MarshmallowInteraction>();
            _fishing ??= FindAnyObjectByType<FishingInteraction>();

            var spot = _rest != null && _rest.IsResting ? _rest.ActiveRestSpot : null;
            var isMarshmallow = spot != null && spot.GetComponent<MarshmallowRitual>() != null;
            var isFishing = spot != null && spot.GetComponent<FishingRitual>() != null;
            SetActive(_panelRoot, isMarshmallow || isFishing);
            SetActive(_marshmallowContent, isMarshmallow);
            SetActive(_fishingContent, isFishing);

            if (isMarshmallow && _marshmallow != null)
            {
                SetText(_statusText, _marshmallow.Status);
                SetText(_primaryActionText, _marshmallow.IsReadyToEat ? "烤好了" : _marshmallow.IsRoasting ? "轻轻翻面" : "取出棉花糖");
                SetText(_secondaryActionText, _marshmallow.IsReadyToEat ? "吃一口" : "先去走走");
            }
            else if (isFishing && _fishing != null)
            {
                SetText(_statusText, _fishing.Status);
                SetText(_primaryActionText, !_fishing.HasRod ? "拟造鱼竿" : _fishing.IsLineCast ? "静候鱼儿" : "抛出鱼线");
                SetText(_secondaryActionText, _fishing.IsFishBiting ? "收起鱼线" : "耐心等候");
            }
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value) target.SetActive(value);
        }
        private static void SetText(Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
