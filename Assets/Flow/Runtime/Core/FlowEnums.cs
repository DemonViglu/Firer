namespace Flow.Runtime
{
    public enum FlowNodeState
    {
        Idle,
        Active,
        Completed,
        Failed
    }

    public enum FlowPortDirection
    {
        Input,
        Output
    }

    public enum FlowPortType
    {
        Execution,
        Data
    }
}
