using System;

namespace NiumaInventory.Data
{
    /// <summary>
    /// 物品实例扩展数据项。
    /// 使用数组保存，避免 Unity JsonUtility 不支持 Dictionary 导致存档丢字段。
    /// </summary>
    [Serializable]
    public sealed class InventoryCustomDataEntry
    {
        /// <summary>
        /// 扩展字段 Key。
        /// 必须带模块或业务前缀，例如 equip_durability、quest_source_id、temp_source。
        /// </summary>
        public string Key;

        /// <summary>
        /// 扩展字段值。
        /// 第一版统一用字符串保存，具体含义由写入该 Key 的模块解释。
        /// </summary>
        public string Value;

        public InventoryCustomDataEntry Clone()
        {
            return new InventoryCustomDataEntry
            {
                Key = Key,
                Value = Value
            };
        }

        /// <summary>
        /// 克隆扩展数据数组。
        /// 用于运行时对象和存档快照之间做显式字段映射，避免共享引用。
        /// </summary>
        public static InventoryCustomDataEntry[] CloneArray(InventoryCustomDataEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<InventoryCustomDataEntry>();
            }

            var result = new InventoryCustomDataEntry[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }
}
