using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 可停留的位置，例如篝火旁、湖边石头或观星台。
    /// </summary>
    public sealed class RestSpot : MonoBehaviour
    {
        private static readonly List<RestSpot> ActiveSpots = new();

        [SerializeField, Min(0.1f)] private float _interactionRadius = 1.75f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveSpots()
        {
            ActiveSpots.Clear();
        }

        public static RestSpot FindNearest(Vector3 position)
        {
            RestSpot nearest = null;
            var nearestDistance = float.PositiveInfinity;

            for (var index = ActiveSpots.Count - 1; index >= 0; index--)
            {
                var spot = ActiveSpots[index];
                if (spot == null)
                {
                    ActiveSpots.RemoveAt(index);
                    continue;
                }

                var distance = (spot.transform.position - position).sqrMagnitude;
                if (distance <= spot._interactionRadius * spot._interactionRadius && distance < nearestDistance)
                {
                    nearest = spot;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void OnEnable()
        {
            if (!ActiveSpots.Contains(this))
            {
                ActiveSpots.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveSpots.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
            Gizmos.DrawSphere(transform.position, _interactionRadius);
        }
    }
}
