using System;
using NiumaInventory.Enum;

namespace NiumaInventory.Data
{
    /// <summary>
    /// 背包物品 UI 表现数据。
    /// UI 只读取该数据，不直接修改运行时对象。
    /// </summary>
    [Serializable]
    public sealed class InventoryItemViewData
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
        /// 显示名称。
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 描述文本。
        /// </summary>
        public string Description;

        /// <summary>
        /// 图标资源地址。
        /// </summary>
        public string IconAddress;

        /// <summary>
        /// 物品类型。
        /// </summary>
        public ItemType ItemType;

        /// <summary>
        /// 物品品质。
        /// </summary>
        public ItemQuality Quality;

        /// <summary>
        /// 物品标签。
        /// </summary>
        public string[] Tags = Array.Empty<string>();

        /// <summary>
        /// 当前数量。
        /// </summary>
        public int Count;

        /// <summary>
        /// 最大堆叠数量。
        /// </summary>
        public int MaxStackCount = 1;

        /// <summary>
        /// 单个物品重量。
        /// </summary>
        public float Weight;

        /// <summary>
        /// 当前堆叠总重量。
        /// 通常由 Weight * Count 得出，UI 不需要重复计算。
        /// </summary>
        public float TotalWeight;

        /// <summary>
        /// 所在容器 ID。
        /// </summary>
        public string ContainerId;

        /// <summary>
        /// 所在格子索引。
        /// </summary>
        public int SlotIndex = -1;

        /// <summary>
        /// 是否缺失物品定义。
        /// 缺失物品 UI 应显示为未知物品，并禁用使用、交易、分解等操作。
        /// </summary>
        public bool IsMissing;

        /// <summary>
        /// 是否被锁定。
        /// </summary>
        public bool IsLocked;

        /// <summary>
        /// 是否允许使用。
        /// </summary>
        public bool CanUse;

        /// <summary>
        /// 是否允许移动。
        /// </summary>
        public bool CanMove;

        /// <summary>
        /// 是否允许丢弃。
        /// </summary>
        public bool CanDiscard;

        /// <summary>
        /// 是否允许交易。
        /// </summary>
        public bool CanTrade;

        /// <summary>
        /// 是否允许出售。
        /// </summary>
        public bool CanSell;

        /// <summary>
        /// 是否允许分解。
        /// </summary>
        public bool CanDecompose;
    }
}
