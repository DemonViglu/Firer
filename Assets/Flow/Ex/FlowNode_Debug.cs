using Flow.Runtime;
using UnityEngine;

namespace Flow.Dev
{
    /// <summary>
    /// 示例：自定义FlowNode
    /// 演示如何绑定多个GameObject、配置参数、使用数据端口
    /// </summary>
    [FlowNode("Debug/Move Object To Target", "Move a bound object toward a target position")]
    [System.Serializable]
    public class FlowNode_Debug : FlowNode
    {
        [SerializeField][FlowBindingKey] private string m_ObjectKey = "MoveTarget";
        [SerializeField][FlowBindingKey] private string m_DestinationKey = "Destination";
        [SerializeField] private float m_Speed = 5.0f;
        [SerializeField] private float m_StopDistance = 0.1f;

        private GameObject m_Object;
        private GameObject m_Destination;

        protected override void DefinePortLayout()
        {
            AddExecutionInput("In");
            AddExecutionOutput("Done");
            AddExecutionOutput("Failed");
            AddDataInput<float>("Speed");
        }

        public override FlowNodeState OnExecute(FlowExecutionContext context)
        {
            m_Object = context.GetGameObject(m_ObjectKey);
            m_Destination = context.GetGameObject(m_DestinationKey);

            if (m_Object == null || m_Destination == null)
            {
                Debug.LogWarning($"[FlowNode_Debug] Binding missing: obj='{m_ObjectKey}' dest='{m_DestinationKey}'");
                TriggerOutput(context, "Failed");
                return FlowNodeState.Completed;
            }

            float overrideSpeed = context.GetInputValue<float>(this, "Speed", 0f);
            if (overrideSpeed > 0f)
            {
                m_Speed = overrideSpeed;
            }

            return FlowNodeState.Active;
        }

        public override void OnTick(FlowExecutionContext context, float deltaTime)
        {
            if (m_Object == null || m_Destination == null)
            {
                state = FlowNodeState.Failed;
                return;
            }

            Vector3 direction = m_Destination.transform.position - m_Object.transform.position;
            float distance = direction.magnitude;

            if (distance <= m_StopDistance)
            {
                TriggerOutput(context, "Done");
                state = FlowNodeState.Completed;
                return;
            }

            m_Object.transform.position += direction.normalized * m_Speed * deltaTime;
        }

        public override void OnExit(FlowExecutionContext context)
        {
            m_Object = null;
            m_Destination = null;
        }
    }
}
