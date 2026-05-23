namespace NiumaInventory.Bridge
{
    /// <summary>
    /// 背包 UI 更新数据。
    /// 只承载版本号、全量面板数据和上一次面板数据，避免 UI 同时面对多套状态源。
    /// </summary>
    public readonly struct InventoryUIUpdate
    {
        /// <summary>更新类型。</summary>
        public readonly InventoryUIUpdateType UpdateType;

        /// <summary>背包模块修订号。</summary>
        public readonly int Revision;

        /// <summary>当前背包面板数据。</summary>
        public readonly InventoryPanelViewData PanelData;

        /// <summary>上一次背包面板数据。</summary>
        public readonly InventoryPanelViewData PreviousPanelData;

        /// <summary>当前是否存在背包面板数据。</summary>
        public bool HasPanelData => PanelData != null;

        public InventoryUIUpdate(
            InventoryUIUpdateType updateType,
            int revision,
            InventoryPanelViewData panelData,
            InventoryPanelViewData previousPanelData)
        {
            UpdateType = updateType;
            Revision = revision;
            PanelData = panelData;
            PreviousPanelData = previousPanelData;
        }
    }
}
