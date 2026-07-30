namespace LegionCore.Properties
{
    // EConfigurableType is a closed, non-extensible 15-value vanilla enum - no mod slot
    // exists without an IL/enum patch. Default strategy is to bypass ManagementInterface
    // entirely for mod config UI rather than fight it; revisit only if that's not enough.
    internal sealed class ConfigurableApi : IConfigurableApi
    {
        public bool IsReady => Readiness.Check();
    }
}
