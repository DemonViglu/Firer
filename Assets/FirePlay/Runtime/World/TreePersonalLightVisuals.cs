using System.Collections.Generic;
using DemonViglu.FirePlay.Save;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    public sealed class TreePersonalLightVisuals : MonoBehaviour
    {
        [SerializeField] private Light _lightPrefab;
        [SerializeField] private Transform _lightRoot;
        [SerializeField, Min(0f)] private float _radius = 2f;
        [SerializeField] private Vector2 _heightRange = new(2f, 5f);

        private readonly List<Light> _instances = new();

        public void Apply(IReadOnlyList<TreePersonalLightRecord> records)
        {
            if (_lightPrefab == null || records == null)
            {
                return;
            }

            while (_instances.Count < records.Count)
            {
                _instances.Add(Instantiate(_lightPrefab, _lightRoot != null ? _lightRoot : transform));
            }

            for (var index = 0; index < _instances.Count; index++)
            {
                var active = index < records.Count;
                _instances[index].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var record = records[index];
                var angle = record.positionSeed * Mathf.PI * 2f;
                var height = Mathf.Lerp(_heightRange.x, _heightRange.y, Mathf.Repeat(record.positionSeed * 7.31f, 1f));
                _instances[index].transform.localPosition = new Vector3(Mathf.Cos(angle) * _radius, height, Mathf.Sin(angle) * _radius);
                _instances[index].color = record.color;
            }
        }
    }
}
