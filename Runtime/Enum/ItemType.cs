namespace NiumaInventory.Enum
{
    /// <summary>
    /// 物品大类。
    /// 只表达物品基础分类，不承载商城、合成、装备属性等业务规则。
    /// </summary>
    public enum ItemType
    {
        /// <summary>
        /// 未定义类型，仅用于默认值和错误保护。
        /// </summary>
        None = 0,

        /// <summary>
        /// 材料物品，通常用于收集、合成或任务需求。
        /// </summary>
        Material = 10,

        /// <summary>
        /// 消耗品，通常使用后会减少数量。
        /// </summary>
        Consumable = 20,

        /// <summary>
        /// 装备物品。穿戴、属性生效等由 NiumaEquipment 管理。
        /// </summary>
        Equipment = 30,

        /// <summary>
        /// 任务物品，通常不可丢弃、不可交易。
        /// </summary>
        Quest = 40,

        /// <summary>
        /// 货币物品，可作为特殊物品存储。
        /// </summary>
        Currency = 50,

        /// <summary>
        /// 关键物品，通常用于剧情、解谜或永久解锁。
        /// </summary>
        KeyItem = 60
    }
}
