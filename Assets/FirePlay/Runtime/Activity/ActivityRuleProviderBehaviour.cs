using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// 地点/地区规则脚本的 Unity 入口。规则只读上下文，返回不可变决定。
    /// </summary>
    public abstract class ActivityRuleProviderBehaviour : MonoBehaviour, IActivityRuleProvider
    {
        public abstract System.Collections.Generic.IEnumerable<ActivityRuleResult> Evaluate(IActivityContext context);
    }
}
