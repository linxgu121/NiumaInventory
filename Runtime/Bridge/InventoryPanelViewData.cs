using System;
using NiumaInventory.Data;

namespace NiumaInventory.Bridge
{
    /// <summary>
    /// 背包面板全量 UI 表现数据。
    /// UI 层只读取该对象，不反向修改背包运行时状态。
    /// </summary>
    [Serializable]
    public sealed class InventoryPanelViewData
    {
        /// <summary>背包模块修订号。</summary>
        public int Revision;

        /// <summary>所有可展示容器。</summary>
        public InventoryContainerViewData[] Containers = Array.Empty<InventoryContainerViewData>();

        /// <summary>所有可展示物品，跨容器平铺。</summary>
        public InventoryItemViewData[] AllItems = Array.Empty<InventoryItemViewData>();

        /// <summary>当前选中容器 ID。</summary>
        public string SelectedContainerId;

        /// <summary>当前选中容器表现数据。</summary>
        public InventoryContainerViewData SelectedContainer;

        /// <summary>当前选中物品实例 ID。</summary>
        public string SelectedItemInstanceId;

        /// <summary>当前选中物品表现数据。</summary>
        public InventoryItemViewData SelectedItem;

        /// <summary>是否存在选中容器。</summary>
        public bool HasSelectedContainer => SelectedContainer != null;

        /// <summary>是否存在选中物品。</summary>
        public bool HasSelectedItem => SelectedItem != null;
    }
}
