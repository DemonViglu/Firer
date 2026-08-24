using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Texture-free radial activity wheel. The Form owns selection semantics;
    /// this component only draws segments and resolves a pointer direction.
    /// It is explicitly authored on the activity selection prefab.
    /// </summary>
    public sealed class ActivityRadialWheelGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float _innerRadius = 86f;
        [SerializeField, Min(1f)] private float _outerRadius = 190f;
        [SerializeField, Range(0f, 18f)] private float _gapDegrees = 4f;
        [SerializeField, Range(2, 12)] private int _arcSubdivisions = 6;
        [SerializeField] private Color _normalColor = new(0.68f, 0.78f, 0.86f, 0.13f);
        [SerializeField] private Color _highlightColor = new(0.92f, 0.97f, 1f, 0.42f);

        private int _segmentCount;
        private int _highlightedIndex = -1;

        public int SegmentCount => _segmentCount;

        public void SetSegments(int count, int highlightedIndex)
        {
            count = Mathf.Clamp(count, 0, 12);
            highlightedIndex = highlightedIndex >= 0 && highlightedIndex < count
                ? highlightedIndex
                : -1;
            if (_segmentCount == count && _highlightedIndex == highlightedIndex)
                return;

            _segmentCount = count;
            _highlightedIndex = highlightedIndex;
            SetVerticesDirty();
        }

        public int GetSegmentAtScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            if (_segmentCount <= 0
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    screenPoint,
                    eventCamera,
                    out var localPoint))
            {
                return -1;
            }

            var radius = localPoint.magnitude;
            if (radius < _innerRadius || radius > _outerRadius)
                return -1;

            var angle = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
            var step = 360f / _segmentCount;
            var firstCenter = 90f - step * 0.5f;
            return Mathf.RoundToInt(Mathf.Repeat(firstCenter - angle, 360f) / step) % _segmentCount;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (_segmentCount <= 0)
                return;

            var maximumRadius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            var outer = Mathf.Min(_outerRadius, maximumRadius);
            var inner = Mathf.Min(_innerRadius, Mathf.Max(0f, outer - 1f));
            var step = 360f / _segmentCount;
            var halfGap = Mathf.Min(_gapDegrees, step * 0.35f) * 0.5f;

            for (var segment = 0; segment < _segmentCount; segment++)
            {
                var center = 90f - step * 0.5f - segment * step;
                var start = center - step * 0.5f + halfGap;
                var end = center + step * 0.5f - halfGap;
                var segmentColor = segment == _highlightedIndex ? _highlightColor : _normalColor;
                var vertexStart = vertexHelper.currentVertCount;

                for (var arc = 0; arc <= _arcSubdivisions; arc++)
                {
                    var angle = Mathf.Lerp(start, end, arc / (float)_arcSubdivisions) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    vertexHelper.AddVert(direction * inner, segmentColor, Vector2.zero);
                    vertexHelper.AddVert(direction * outer, segmentColor, Vector2.one);
                }

                for (var arc = 0; arc < _arcSubdivisions; arc++)
                {
                    var index = vertexStart + arc * 2;
                    vertexHelper.AddTriangle(index, index + 3, index + 1);
                    vertexHelper.AddTriangle(index, index + 2, index + 3);
                }
            }
        }
    }
}
