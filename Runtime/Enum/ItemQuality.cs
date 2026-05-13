namespace NiumaInventory.Enum
{
    /// <summary>
    /// 物品品质。
    /// 品质主要用于 UI 表现和排序，不直接代表价格、战斗数值或掉率。
    /// </summary>
    public enum ItemQuality
    {
        /// <summary>
        /// 普通。
        /// </summary>
        Common = 0,

        /// <summary>
        /// 优秀。
        /// </summary>
        Uncommon = 10,

        /// <summary>
        /// 稀有。
        /// </summary>
        Rare = 20,

        /// <summary>
        /// 史诗。
        /// </summary>
        Epic = 30,

        /// <summary>
        /// 传说。
        /// </summary>
        Legendary = 40,

        /// <summary>
        /// 剧情专用品质。
        /// </summary>
        Story = 50
    }
}
