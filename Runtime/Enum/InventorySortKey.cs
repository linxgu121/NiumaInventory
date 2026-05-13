namespace NiumaInventory.Enum
{
    /// <summary>
    /// 背包整理排序维度。
    /// 多个维度按数组顺序依次比较。
    /// </summary>
    public enum InventorySortKey
    {
        /// <summary>
        /// 不指定排序维度。
        /// </summary>
        None = 0,

        /// <summary>
        /// 按物品类型排序。
        /// </summary>
        ItemType = 10,

        /// <summary>
        /// 按物品品质排序。
        /// </summary>
        Quality = 20,

        /// <summary>
        /// 按物品 ID 排序。
        /// </summary>
        ItemId = 30,

        /// <summary>
        /// 按数量排序。
        /// </summary>
        Count = 40,

        /// <summary>
        /// 按获得顺序排序。
        /// 第一阶段只定义协议，具体顺序来源由核心服务实现。
        /// </summary>
        AcquiredOrder = 50
    }
}
