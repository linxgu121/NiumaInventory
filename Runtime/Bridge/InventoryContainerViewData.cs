using System;
using NiumaInventory.Data;
using NiumaInventory.Enum;

namespace NiumaInventory.Bridge
{
    /// <summary>
    /// 背包容器 UI 表现数据。
    /// UI 只读取该数据，不直接修改容器运行时对象。
    /// </summary>
    [Serializable]
    public sealed class InventoryContainerViewData
    {
        /// <summary>容器稳定 ID。</summary>
        public string ContainerId;

        /// <summary>容器显示名称。</summary>
        public string DisplayName;

        /// <summary>容器类型。</summary>
        public InventoryContainerType ContainerType;

        /// <summary>格子数量。</summary>
        public int SlotCount;

        /// <summary>最大重量。小于等于 0 表示不启用重量上限。</summary>
        public float MaxWeight;

        /// <summary>当前重量。</summary>
        public float CurrentWeight;

        /// <summary>容器是否解锁。</summary>
        public bool IsUnlocked;

        /// <summary>该容器中的物品表现数据。</summary>
        public InventoryItemViewData[] Items = Array.Empty<InventoryItemViewData>();
    }
}
