namespace NiumaInventory.Enum
{
    /// <summary>
    /// 背包容器类型。
    /// 容器类型用于区分数据分区，不等同于 UI 页签。
    /// </summary>
    public enum InventoryContainerType
    {
        /// <summary>
        /// 未定义容器类型。
        /// </summary>
        None = 0,

        /// <summary>
        /// 主背包容器。
        /// </summary>
        Main = 10,

        /// <summary>
        /// 任务物品容器。
        /// </summary>
        Quest = 20,

        /// <summary>
        /// 货币容器。
        /// </summary>
        Currency = 30,

        /// <summary>
        /// 临时容器，用于拾取预览、奖励缓存或溢出承接。
        /// </summary>
        Temporary = 40,

        /// <summary>
        /// 装备缓存容器。穿戴逻辑由 NiumaEquipment 管理。
        /// </summary>
        Equipment = 50
    }
}
