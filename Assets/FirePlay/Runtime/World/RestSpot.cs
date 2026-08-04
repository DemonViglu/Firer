using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Player;
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
        [SerializeField] private string _restHint;
        [SerializeField] private string _shortRestHint;

        public float InteractionRadius => _interactionRadius;

        /// <summary>
        /// Scene composition hook for modules that react to sitting at this
        /// spot. RestSpot does not know which Activity is selected; it only
        /// reports the lifecycle fact.
        /// </summary>
        public event Action<RestInteraction> RestStarted;
        public event Action<RestInteraction> RestEnded;

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

        public void NotifyRestStarted(RestInteraction interaction)
        {
            RestStarted?.Invoke(interaction);
        }

        public void NotifyRestEnded(RestInteraction interaction)
        {
            RestEnded?.Invoke(interaction);
        }

        public string GetRestHint(bool shortForm)
        {
            var configuredHint = shortForm ? _shortRestHint : _restHint;
            if (!string.IsNullOrWhiteSpace(configuredHint))
                return configuredHint;

            return shortForm ? " · 这里可以坐下歇一会儿" : "这里可以坐下，安静歇一会儿";
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
            Gizmos.DrawSphere(transform.position, _interactionRadius);
        }
    }
}
