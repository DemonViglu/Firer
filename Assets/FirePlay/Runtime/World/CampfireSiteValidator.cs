using DemonViglu.FirePlay.Data;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    public static class CampfireSiteValidator
    {
        public static bool TryValidate(
            RaycastHit hit,
            Vector3 playerPosition,
            SmallFireConfig config,
            out string reason)
        {
            if (config == null)
            {
                reason = "Missing config";
                return false;
            }

            var distance = Vector3.Distance(playerPosition, hit.point);
            if (distance > config.MaximumPlacementDistance)
            {
                reason = "Too far";
                return false;
            }

            var slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > config.MaximumSlopeDegrees)
            {
                reason = "Slope too steep";
                return false;
            }

            reason = "Valid";
            return true;
        }
    }
}
