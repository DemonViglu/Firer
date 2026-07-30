using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Prefab-driven visibility controller for world action buttons. It owns no
    /// layout or gameplay; place it on always-active UIControllers.
    /// </summary>
    public sealed class FirePlayContextActionsPanel : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction _interaction;
        [SerializeField] private LocalPlayerContext _localPlayer;
        [SerializeField] private GameObject _panelRoot;
        [Header("Action buttons")]
        [SerializeField] private GameObject _gatherEmberButton;
        [SerializeField] private GameObject _addFireButton;
        [SerializeField] private GameObject _drawFireButton;
        [SerializeField] private GameObject _reclaimSmallFireButton;
        [SerializeField] private GameObject _contributeWorldTreeButton;
        [SerializeField] private GameObject _restButton;

        private void Update()
        {
            _localPlayer ??= LocalPlayerContext.Current;
            _interaction ??= _localPlayer != null ? _localPlayer.Interaction : null;
            var hasFlameSource = _interaction != null && _interaction.NearestFlameSource != null && _interaction.NearestFlameSource.IsAvailable;
            var hasCampfire = _interaction != null && _interaction.NearestCampfire != null;
            var hasSmallFire = _interaction != null && _interaction.NearestSmallFire != null;
            var hasWorldTree = _interaction != null && _interaction.NearestWorldTree != null;
            var hasRestSpot = _interaction != null && _interaction.NearestRestSpot != null;

            SetActive(_gatherEmberButton, hasFlameSource);
            SetActive(_addFireButton, hasCampfire || hasSmallFire);
            SetActive(_drawFireButton, hasCampfire);
            SetActive(_reclaimSmallFireButton, hasSmallFire);
            SetActive(_contributeWorldTreeButton, hasWorldTree && !_interaction.NearestWorldTree.HasLocalContribution);
            SetActive(_restButton, hasRestSpot);
            // This component belongs on the always-active UIController. Never
            // default PanelRoot to this GameObject: that would disable every UI
            // controller on the first frame before the player detects a target.
            // Android's startup order exposed this race most consistently.
            if (_panelRoot != null)
            {
                SetActive(_panelRoot, hasFlameSource || hasCampfire || hasSmallFire || hasWorldTree || hasRestSpot);
            }
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }
}
