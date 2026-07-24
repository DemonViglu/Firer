namespace DemonViglu.FirePlay.Core
{
    /// <summary>
    /// FirePlay 着色器属性的唯一入口。
    /// 使用整数 ID 可避免每帧按字符串查询属性。
    /// </summary>
    public static class FirePlayShaderPropertyIds
    {
        public static readonly int LitAmount = UnityEngine.Shader.PropertyToID("_LitAmount");
        public static readonly int InkColor = UnityEngine.Shader.PropertyToID("_InkColor");
        public static readonly int BaseColor = UnityEngine.Shader.PropertyToID("_BaseColor");
        public static readonly int BloomColor = UnityEngine.Shader.PropertyToID("_BloomColor");
        public static readonly int FlameColor = UnityEngine.Shader.PropertyToID("_FlameColor");
        public static readonly int FlameIntensity = UnityEngine.Shader.PropertyToID("_FlameIntensity");
    }
}
