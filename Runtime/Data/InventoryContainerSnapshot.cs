using System;
using NiumaInventory.Enum;

namespace NiumaInventory.Data
{
    /// <summary>
    /// 背包容器存档快照。
    /// 只保存稳定 ID 和轻量状态。
    /// </summary>
    [Serializable]
    public sealed class InventoryContainerSnapshot
    {
        /// <summary>
        /// 容器稳定 ID。
        /// </summary>
        public string ContainerId;

        /// <summary>
        /// 保存时的容器类型。
        /// </summary>
        public InventoryContainerType ContainerType;

        /// <summary>
        /// 保存时的格子数量。
        /// </summary>
        public int SlotCount;

        /// <summary>
        /// 保存时的最大重量。
        /// </summary>
        public float MaxWeight;

        /// <summary>
        /// 导出时的当前重量冗余值。
        /// 读档时不能直接信任，应由背包服务重新计算并覆盖。
        /// </summary>
        public float CurrentWeight;

        /// <summary>
        /// 保存时容器是否解锁。
        /// </summary>
        public bool IsUnlocked;
    }
}
