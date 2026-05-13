namespace NiumaInventory.Enum
{
    /// <summary>
    /// 背包操作失败原因。
    /// UI 和业务模块应读取枚举，不要通过字符串匹配失败原因。
    /// </summary>
    public enum InventoryFailureReason
    {
        /// <summary>
        /// 没有失败。
        /// </summary>
        None = 0,

        /// <summary>
        /// 请求参数无效。
        /// </summary>
        InvalidRequest = 10,

        /// <summary>
        /// 找不到物品静态定义。
        /// </summary>
        ItemDefinitionMissing = 20,

        /// <summary>
        /// 找不到目标容器。
        /// </summary>
        ContainerMissing = 30,

        /// <summary>
        /// 格子索引无效。
        /// </summary>
        SlotInvalid = 40,

        /// <summary>
        /// 目标格子已被占用，且当前请求不允许交换或合并。
        /// </summary>
        SlotOccupied = 50,

        /// <summary>
        /// 找不到目标物品实例。
        /// </summary>
        ItemNotFound = 60,

        /// <summary>
        /// 物品数量不足。
        /// </summary>
        NotEnoughCount = 70,

        /// <summary>
        /// 背包或容器格子已满。
        /// </summary>
        InventoryFull = 80,

        /// <summary>
        /// 超过容器重量限制。
        /// </summary>
        WeightLimitExceeded = 90,

        /// <summary>
        /// 物品被锁定，不能执行当前操作。
        /// </summary>
        ItemLocked = 100,

        /// <summary>
        /// 物品不允许移动。
        /// </summary>
        ItemCannotMove = 110,

        /// <summary>
        /// 物品不允许丢弃。
        /// </summary>
        ItemCannotDiscard = 120,

        /// <summary>
        /// 物品不允许使用。
        /// </summary>
        ItemCannotUse = 130,

        /// <summary>
        /// 唯一物品已经持有，不能再次加入。
        /// </summary>
        UniqueItemAlreadyOwned = 140,

        /// <summary>
        /// 容器不支持该物品类型或标签。
        /// </summary>
        UnsupportedItemType = 150
    }
}
