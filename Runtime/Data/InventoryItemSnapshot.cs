using System;

namespace NiumaInventory.Data
{
    /// <summary>
    /// 背包物品存档快照。
    /// 只保存稳定 ID 和运行时事实，不保存显示名称、图标、品质等静态配置。
    /// </summary>
    [Serializable]
    public sealed class InventoryItemSnapshot
    {
        /// <summary>
        /// 物品实例 ID。
        /// </summary>
        public string InstanceId;

        /// <summary>
        /// 物品静态 ID。
        /// </summary>
        public string ItemId;

        /// <summary>
        /// 当前数量。
        /// </summary>
        public int Count;

        /// <summary>
        /// 所在容器 ID。
        /// </summary>
        public string ContainerId;

        /// <summary>
        /// 所在格子索引。
        /// </summary>
        public int SlotIndex = -1;

        /// <summary>
        /// 是否被锁定。
        /// </summary>
        public bool IsLocked;

        /// <summary>
        /// 是否缺失物品定义。
        /// 读档时 ItemId 找不到定义会被标记为 true，方便 UI 和逻辑层做保护。
        /// </summary>
        public bool IsMissing;

        /// <summary>
        /// 获得顺序。
        /// 用于稳定排序，读档后应继承旧值。
        /// </summary>
        public long AcquiredOrder;

        /// <summary>
        /// 轻量扩展数据。
        /// </summary>
        public InventoryCustomDataEntry[] CustomData = Array.Empty<InventoryCustomDataEntry>();
    }
}
