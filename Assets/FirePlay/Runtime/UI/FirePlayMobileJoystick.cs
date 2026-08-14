using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Touch joystick presentation. It owns no player movement logic: it reports a
    /// normalized vector to FirePlayMobileInputRouter and can therefore be restyled
    /// or replaced without touching the player controller.
    /// </summary>
    public sealed class FirePlayMobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private FirePlayMobileInputRouter _inputRouter;
        [SerializeField] private RectTransform _joystickArea;
        [SerializeField] private RectTransform _handle;
        [SerializeField, Min(1f)] private float _handleRange = 72f;

        private void Awake()
        {
            _joystickArea ??= transform as RectTransform;
            // The handle is visual-only. If it receives a raycast, a touch that
            // begins on its centre never reaches this parent's pointer handlers,
            // so dragging appears to do nothing on the most natural touch target.
            var handleImage = _handle != null ? _handle.GetComponent<Image>() : null;
            if (handleImage != null)
            {
                handleImage.raycastTarget = false;
            }
            CenterHandle();
        }

        public void Configure(FirePlayMobileInputRouter inputRouter, RectTransform joystickArea, RectTransform handle)
        {
            _inputRouter = inputRouter;
            _joystickArea = joystickArea;
            _handle = handle;
            CenterHandle();
        }

        public void OnPointerDown(PointerEventData eventData) => UpdatePosition(eventData);
        public void OnDrag(PointerEventData eventData) => UpdatePosition(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            _inputRouter?.SetMove(Vector2.zero);
            CenterHandle();
        }

        private void UpdatePosition(PointerEventData eventData)
        {
            if (_joystickArea == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _joystickArea, eventData.position, eventData.pressEventCamera, out var localPosition))
            {
                return;
            }

            var radius = Mathf.Max(1f, _handleRange);
            var value = Vector2.ClampMagnitude(localPosition / radius, 1f);
            if (_handle != null)
            {
                _handle.anchoredPosition = value * radius;
            }

            _inputRouter?.SetMove(value);
        }

        private void CenterHandle()
        {
            if (_handle != null)
            {
                _handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
