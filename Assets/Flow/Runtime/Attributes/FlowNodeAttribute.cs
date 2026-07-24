using System;

namespace Flow.Runtime
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class FlowNodeAttribute : Attribute
    {
        public string menuPath;
        public string description;

        public FlowNodeAttribute(string menuPath, string description = "")
        {
            this.menuPath = menuPath;
            this.description = description;
        }
    }
}
