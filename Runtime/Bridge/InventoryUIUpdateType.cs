namespace NiumaInventory.Bridge
{
    /// <summary>
    /// 背包 UI 更新类型。
    /// </summary>
    public enum InventoryUIUpdateType
    {
        /// <summary>无更新。</summary>
        None = 0,

        /// <summary>全量刷新背包面板。</summary>
        Refresh = 1,

        /// <summary>背包服务不可用或当前没有可展示数据。</summary>
        Cleared = 2
    }
}
