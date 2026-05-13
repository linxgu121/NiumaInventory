using System;
using NiumaInventory.Data;

namespace NiumaInventory.Request
{
    /// <summary>
    /// 添加物品请求。
    /// 用于拾取、任务奖励、商城购买、合成产物等入口。
    /// </summary>
    [Serializable]
    public sealed class AddItemRequest
    {
        /// <summary>
        /// 要添加的物品 ID。
        /// </summary>
        public string ItemId;

        /// <summary>
        /// 要添加的数量。
        /// 必须大于 0。
        /// </summary>
        public int Count = 1;

        /// <summary>
        /// 目标容器 ID。
        /// 为空时由背包服务按规则自动选择容器。
        /// </summary>
        public string TargetContainerId;

        /// <summary>
        /// 目标格子索引。
        /// 小于 0 表示由背包服务自动寻找格子。
        /// </summary>
        public int TargetSlotIndex = -1;

        /// <summary>
        /// 是否允许部分成功。
        /// false 时必须全部放入，否则整批失败且不修改背包。
        /// </summary>
        public bool AllowPartial;

        /// <summary>
        /// 新实例初始扩展数据。
        /// 临时容器来源建议写入 temp_source。
        /// </summary>
        public InventoryCustomDataEntry[] CustomData = Array.Empty<InventoryCustomDataEntry>();

        /// <summary>
        /// 请求来源模块名。
        /// 仅用于日志、调试和排查链路，不作为业务判断主键。
        /// </summary>
        public string SourceModule;
    }
}
