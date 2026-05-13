using System;
using NiumaInventory.Enum;
using UnityEngine;

namespace NiumaInventory.Config
{
    /// <summary>
    /// 背包容器配置。
    /// 容器是背包的数据分区，不等同于 UI 页签。
    /// </summary>
    [CreateAssetMenu(menuName = "NiumaInventory/Container Config", fileName = "InventoryContainerConfig")]
    public sealed class InventoryContainerConfig : ScriptableObject
    {
        [Tooltip("容器稳定 ID。例如 main、quest、currency、temporary。")]
        public string ContainerId;

        [Tooltip("容器显示名称。后续接本地化时可以改成本地化 Key。")]
        public string DisplayName;

        [Tooltip("容器类型。只用于基础分区，不承载 UI 页签规则。")]
        public InventoryContainerType ContainerType = InventoryContainerType.Main;

        [Tooltip("格子数量。小于等于 0 表示该容器不可放入物品。")]
        public int SlotCount = 20;

        [Tooltip("最大重量。小于等于 0 表示不启用重量上限。")]
        public float MaxWeight;

        [Tooltip("允许放入的物品类型。为空表示不按类型白名单限制。")]
        public ItemType[] AcceptedItemTypes = Array.Empty<ItemType>();

        [Tooltip("允许放入的标签白名单。为空表示不按标签白名单限制。")]
        public string[] AcceptedTags = Array.Empty<string>();

        [Tooltip("禁止放入的标签黑名单。物品同时命中 AcceptedTags 和 RejectTags 时，RejectTags 优先。")]
        public string[] RejectTags = Array.Empty<string>();

        [Tooltip("系统自动加入物品时，是否允许优先填充该容器内已有未满堆叠。")]
        public bool AllowAutoStack = true;

        [Tooltip("是否允许玩家手动把物品移入或移出该容器。还需要物品自身 CanMove 为 true。")]
        public bool AllowManualMove = true;

        [Tooltip("容器初始是否解锁。锁定容器不会参与常规添加和移动。")]
        public bool IsUnlockedByDefault = true;
    }
}
