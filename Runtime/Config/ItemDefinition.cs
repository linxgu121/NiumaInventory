using System;
using NiumaInventory.Enum;
using UnityEngine;

namespace NiumaInventory.Config
{
    /// <summary>
    /// 物品静态定义。
    /// 只描述“这是什么物品”，不保存玩家持有状态。
    /// </summary>
    [CreateAssetMenu(menuName = "NiumaInventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("物品稳定 ID。正式内容中不要随意修改，不要使用资源名或显示文本作为唯一 ID。")]
        public string ItemId;

        [Tooltip("物品显示名称。后续接本地化时可以改成本地化 Key。")]
        public string DisplayName;

        [Tooltip("物品描述文本。用于背包 Tips、奖励预览等 UI。")]
        [TextArea]
        public string Description;

        [Tooltip("图标资源地址。建议使用字符串，后续可直接作为 Addressables Key，不要直接保存 Sprite 引用。")]
        public string IconAddress;

        [Tooltip("物品大类。只表达基础分类，不承载商城、合成或装备属性规则。")]
        public ItemType ItemType = ItemType.Material;

        [Tooltip("物品品质。主要用于 UI 表现和排序，不直接决定价格或数值。")]
        public ItemQuality Quality = ItemQuality.Common;

        [Tooltip("物品标签。统一使用小写英文、数字和下划线，建议以 tag_ 开头。")]
        public string[] Tags = Array.Empty<string>();

        [Tooltip("最大堆叠数量。小于等于 1 表示不可堆叠；唯一物品即使误填大于 1，运行时也按 1 处理。")]
        public int MaxStackCount = 1;

        [Tooltip("单个物品重量。小于等于 0 表示不占重量。")]
        public float Weight;

        [Tooltip("是否允许玩家丢弃。任务物品和关键物品通常不允许。")]
        public bool CanDiscard = true;

        [Tooltip("是否允许交易。商城、交易行或 NPC 交易会读取该基础规则。")]
        public bool CanTrade = true;

        [Tooltip("是否允许出售。出售价格由商城或经济模块决定，背包只保存基础开关。")]
        public bool CanSell = true;

        [Tooltip("是否允许分解。分解配方和产物由后续模块决定。")]
        public bool CanDecompose = true;

        [Tooltip("是否允许邮寄。后续邮件模块读取该基础规则。")]
        public bool CanMail = true;

        [Tooltip("是否允许直接使用。使用条件和具体效果由外部业务模块判断。")]
        public bool CanUse;

        [Tooltip("是否允许玩家手动移动。容器也可以通过 AllowManualMove 进一步限制。")]
        public bool CanMove = true;

        [Tooltip("是否为唯一物品。唯一物品在整个背包所有容器中只能存在一个同 ItemId 实例。")]
        public bool IsUnique;
    }
}
