using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 明确标记允许放置小火种的地面，避免射线命中玩家、装饰物或其他火种。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CampfirePlacementSurface : MonoBehaviour
    {
    }
}
