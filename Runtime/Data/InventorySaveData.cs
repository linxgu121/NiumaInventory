using System;

namespace NiumaInventory.Data
{
    /// <summary>
    /// 背包模块存档数据。
    /// 作为 NiumaSave 中 inventory section 的业务快照。
    /// </summary>
    [Serializable]
    public sealed class InventorySaveData
    {
        /// <summary>
        /// 背包存档结构版本。
        /// 用于后续迁移旧存档。
        /// </summary>
        public int Version = 1;

        /// <summary>
        /// 背包全局修订号。
        /// 读档后继承该值，并在后续变更时继续递增。
        /// </summary>
        public int Revision;

        /// <summary>
        /// 容器快照。
        /// </summary>
        public InventoryContainerSnapshot[] Containers = Array.Empty<InventoryContainerSnapshot>();

        /// <summary>
        /// 物品实例快照。
        /// </summary>
        public InventoryItemSnapshot[] Items = Array.Empty<InventoryItemSnapshot>();
    }
}
