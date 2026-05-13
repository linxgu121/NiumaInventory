using System;
using NiumaInventory.Enum;

namespace NiumaInventory.Request
{
    /// <summary>
    /// 整理容器请求。
    /// 排序会改变 SlotIndex，因此属于背包数据修改，成功后需要递增 Revision。
    /// </summary>
    [Serializable]
    public sealed class SortContainerRequest
    {
        /// <summary>
        /// 要整理的容器 ID。
        /// </summary>
        public string ContainerId;

        /// <summary>
        /// 排序维度。
        /// 多个维度按数组顺序依次比较。
        /// </summary>
        public InventorySortKey[] SortKeys = Array.Empty<InventorySortKey>();

        /// <summary>
        /// 是否保持锁定物品原格子。
        /// </summary>
        public bool KeepLockedSlot = true;

        /// <summary>
        /// 请求来源模块名。
        /// </summary>
        public string SourceModule;
    }
}
