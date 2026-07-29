using DemonViglu.FirePlay.World;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Prefab-driven placement overlay. Its full-screen background can be a Button:
    /// tapping empty space cancels placement, while a higher child confirms it.
    /// </summary>
    public sealed class FirePlayPlacementPanel : MonoBehaviour
    {
        [SerializeField] private CampfirePlacement _placement;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Text _statusText;

        private void Awake() => _panelRoot ??= gameObject;

        private void Update()
        {
            _placement ??= FindAnyObjectByType<CampfirePlacement>();
            var visible = _placement != null && _placement.IsPlacing;
            if (_panelRoot.activeSelf != visible)
            {
                _panelRoot.SetActive(visible);
            }

            if (visible && _statusText != null)
            {
                _statusText.text = _placement.PlacementStatus;
            }
        }
    }
}
