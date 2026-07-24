using Flow.Runtime;
using UnityEngine;

namespace Flow.Dev
{
    /// <summary>
    /// 示例1：多输入多输出 + 判断哪个输入触发
    /// 一个门控节点：Execute 正常通过，Reset 重置状态
    /// Category 用 "Logic" → 绿色标题
    /// </summary>
    [FlowNode("Logic/Gate", "A gate that can be opened/closed and reset")]
    [System.Serializable]
    public class FlowNode_Gate : FlowNode
    {
        [SerializeField] private bool m_StartOpen = true;

        [System.NonSerialized] private bool m_IsOpen;

        protected override void DefinePortLayout()
        {
            AddExecutionInput("Execute");
            AddExecutionInput("Open");
            AddExecutionInput("Close");
            AddExecutionOutput("Out");
        }

        public override void OnInitialize(FlowExecutionContext context)
        {
            m_IsOpen = m_StartOpen;
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            var port = GetPort(triggeredPortId);
            string portName = port != null ? port.portName : "Execute";

            switch (portName)
            {
                case "Open":
                    m_IsOpen = true;
                    break;
                case "Close":
                    m_IsOpen = false;
                    break;
                case "Execute":
                    if (m_IsOpen)
                    {
                        TriggerOutput(context, "Out");
                    }
                    break;
            }

            return FlowNodeState.Completed;
        }
    }

    /// <summary>
    /// 示例2：持续性节点（类似 Wait）
    /// 等待一个绑定的 Animator 播完某个动画
    /// Category 用 "Action" → 橙色标题
    /// </summary>
    [FlowNode("Action/Wait Animation", "Wait for an animator state to finish")]
    [System.Serializable]
    public class FlowNode_WaitAnimation : FlowNode
    {
        [SerializeField] private string m_AnimatorKey = "Animator";
        [SerializeField] private string m_StateName = "Attack";
        [SerializeField] private int m_Layer = 0;

        [System.NonSerialized] private Animator m_Animator;
        [System.NonSerialized] private bool m_WaitingForState;

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Done");
            AddExecutionOutput("Failed");
            AddDataInput<string>("State");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            m_Animator = context.GetBinding<Animator>(m_AnimatorKey);
            if (m_Animator == null)
            {
                Debug.LogWarning($"[Flow] WaitAnimation: No Animator bound for key '{m_AnimatorKey}'");
                TriggerOutput(context, "Failed");
                return FlowNodeState.Completed;
            }

            string stateName = context.GetInputValue<string>(this, "State", m_StateName);
            m_Animator.Play(stateName, m_Layer);
            m_WaitingForState = true;

            // 返回 Active = 持续性节点，每帧会调 OnTick
            return FlowNodeState.Active;
        }

        public override void OnTick(FlowExecutionContext context, float deltaTime)
        {
            if (m_Animator == null)
            {
                state = FlowNodeState.Failed;
                return;
            }

            var stateInfo = m_Animator.GetCurrentAnimatorStateInfo(m_Layer);

            // 等待动画进入目标状态
            if (m_WaitingForState)
            {
                if (stateInfo.IsName(m_StateName))
                {
                    m_WaitingForState = false;
                }
                return;
            }

            // 动画播放完毕（normalizedTime >= 1）
            if (stateInfo.normalizedTime >= 1.0f)
            {
                TriggerOutput(context, "Done");
                state = FlowNodeState.Completed;
            }
        }

        public override void OnExit(FlowExecutionContext context)
        {
            m_Animator = null;
            m_WaitingForState = false;
        }
    }

    /// <summary>
    /// 示例3：多输出分支 + 数据端口
    /// 根据血量百分比走不同分支
    /// Category 用 "Logic" → 绿色标题
    /// </summary>
    [FlowNode("Logic/Health Branch", "Branch based on health percentage thresholds")]
    [System.Serializable]
    public class FlowNode_HealthBranch : FlowNode
    {
        [SerializeField] private float m_LowThreshold = 0.3f;
        [SerializeField] private float m_HighThreshold = 0.7f;

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddDataInput<float>("Health");
            AddExecutionOutput("High");
            AddExecutionOutput("Medium");
            AddExecutionOutput("Low");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            float health = context.GetInputValue<float>(this, "Health", 1.0f);

            if (health >= m_HighThreshold)
            {
                TriggerOutput(context, "High");
            }
            else if (health >= m_LowThreshold)
            {
                TriggerOutput(context, "Medium");
            }
            else
            {
                TriggerOutput(context, "Low");
            }

            return FlowNodeState.Completed;
        }
    }
}
