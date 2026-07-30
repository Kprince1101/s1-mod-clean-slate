namespace LegionCore.Npcs
{
    // Dealer-redirect (Contract.DeliveryLocation swap) and behaviour-stack helpers land here
    // once Clean Slate's M3 ticket actually builds them - GUIDManager internals aren't
    // confirmed yet, so nothing here fakes a redirect prematurely.
    internal sealed class NpcApi : INpcApi
    {
        public bool IsReady => Readiness.Check();
    }
}
