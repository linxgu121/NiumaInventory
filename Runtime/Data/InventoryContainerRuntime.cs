using System;
using NiumaInventory.Enum;

namespace NiumaInventory.Data
{
    /// <summary>
    /// 背包容器运行时状态。
    /// 当前重量是运行时计算值，读档后应由物品数据重新计算。
    /// </summary>
    [Serializable]
    public sealed class InventoryContainerRuntime
    {
        /// <summary>
        /// 容器稳定 ID。
        /// </summary>
        public string ContainerId;

        /// <summary>
        /// 容器类型。
        /// </summary>
        public InventoryContainerType ContainerType;

        /// <summary>
        /// 格子数量。
        /// </summary>
        public int SlotCount;

        /// <summary>
        /// 最大重量。小于等于 0 表示不启用重量上限。
        /// </summary>
        public float MaxWeight;

        /// <summary>
        /// 当前重量。
        /// 该值只能由背包服务根据物品定义重新计算。
        /// </summary>
        public float CurrentWeight;

        /// <summary>
        /// 容器是否已经解锁。
        /// </summary>
        public bool IsUnlocked = true;

        public InventoryContainerSnapshot ToSnapshot()
        {
            return new InventoryContainerSnapshot
            {
                ContainerId = ContainerId,
                ContainerType = ContainerType,
                SlotCount = SlotCount,
                MaxWeight = MaxWeight,
                CurrentWeight = CurrentWeight,
                IsUnlocked = IsUnlocked
            };
        }
    }
}
