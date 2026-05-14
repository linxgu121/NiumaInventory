using System;

namespace NiumaInventory.Data
{
    /// <summary>
    /// 背包物品运行时实例。
    /// 描述玩家当前持有的某一堆或某一个物品。
    /// </summary>
    [Serializable]
    public sealed class InventoryItemRuntime
    {
        /// <summary>
        /// 实例稳定 ID。
        /// 创建新实例时生成，读档时必须沿用旧值。
        /// </summary>
        public string InstanceId;

        /// <summary>
        /// 物品静态 ID，对应 ItemDefinition.ItemId。
        /// </summary>
        public string ItemId;

        /// <summary>
        /// 当前数量。
        /// 不可堆叠物品通常为 1。
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
        /// 锁定状态属于背包通用字段，不写入 CustomData。
        /// </summary>
        public bool IsLocked;

        /// <summary>
        /// 是否缺失物品定义。
        /// 缺失物品保留实例和数量，但默认不允许使用、交易、移动到特殊容器。
        /// </summary>
        public bool IsMissing;

        /// <summary>
        /// 获得顺序。
        /// 新实例创建时递增生成，读档时沿用旧值，用于稳定的获得顺序排序。
        /// </summary>
        public long AcquiredOrder;

        /// <summary>
        /// 运行时标记。
        /// 第一版只预留字段，不建议业务模块直接依赖具体位含义。
        /// 该字段不进入存档快照，读档或运行中重建服务后会丢失。
        /// </summary>
        public int RuntimeFlags;

        /// <summary>
        /// 轻量扩展数据。
        /// 背包只负责保存和随实例移动，不解释具体业务含义。
        /// </summary>
        public InventoryCustomDataEntry[] CustomData = Array.Empty<InventoryCustomDataEntry>();

        /// <summary>
        /// 显式导出存档快照。
        /// 不要直接序列化运行时对象，以免未来加入缓存字段后污染存档。
        /// </summary>
        public InventoryItemSnapshot ToSnapshot()
        {
            return new InventoryItemSnapshot
            {
                InstanceId = InstanceId,
                ItemId = ItemId,
                Count = Count,
                ContainerId = ContainerId,
                SlotIndex = SlotIndex,
                IsLocked = IsLocked,
                IsMissing = IsMissing,
                AcquiredOrder = AcquiredOrder,
                CustomData = InventoryCustomDataEntry.CloneArray(CustomData)
            };
        }
    }
}
