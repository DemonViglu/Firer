using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DemonViglu.FirePlay.World;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>Transparent right-side touch area that forwards drag deltas as look input.</summary>
    public sealed class FirePlayMobileLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private FirePlayMobileInputRouter _inputRouter;
        [SerializeField, Min(0.01f)] private float _dragSensitivity = 1f;
        [SerializeField] private CampfirePlacement _placement;

        private Graphic _graphic;

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
        }

        private void Update()
        {
            _placement ??= FindAnyObjectByType<CampfirePlacement>();
            // The full-screen placement overlay owns blank-space taps while
            // placing. Otherwise this transparent graphic would swallow those
            // taps before the overlay Button can cancel the placement.
            if (_graphic != null)
            {
                _graphic.raycastTarget = _placement == null || !_placement.IsPlacing;
            }
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            _inputRouter?.AddLookDelta(eventData.delta * _dragSensitivity);
        }
    }
}
