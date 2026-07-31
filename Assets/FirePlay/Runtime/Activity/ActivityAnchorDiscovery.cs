using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Player-side nearby Anchor discovery. It has no UI or activity branches;
    /// a selection surface can poll it while open and rebuild only when the
    /// nearest Anchor identity changes.
    /// </summary>
    public sealed class ActivityAnchorDiscovery
    {
        private readonly float _maximumDistance;
        private bool _initialized;

        public ActivityAnchorNode Current { get; private set; }

        public ActivityAnchorDiscovery(float maximumDistance)
        {
            _maximumDistance = Mathf.Max(0.1f, maximumDistance);
        }

        public bool Refresh(Vector3 playerPosition)
        {
            var next = ActivityAnchorNode.FindNearest(playerPosition, _maximumDistance);
            var changed = !_initialized || !SameAnchor(Current, next);
            Current = next;
            _initialized = true;
            return changed;
        }

        private static bool SameAnchor(ActivityAnchorNode left, ActivityAnchorNode right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            return string.Equals(left.AnchorId, right.AnchorId, System.StringComparison.Ordinal);
        }
    }
}
