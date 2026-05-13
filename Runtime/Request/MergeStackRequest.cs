using System;

namespace NiumaInventory.Request
{
    /// <summary>
    /// 合并堆叠请求。
    /// 只允许同 ItemId 且可堆叠的物品合并。
    /// </summary>
    [Serializable]
    public sealed class MergeStackRequest
    {
        /// <summary>
        /// 源物品实例 ID。
        /// </summary>
        public string SourceInstanceId;

        /// <summary>
        /// 目标物品实例 ID。
        /// 如果为空，则按目标容器和目标格子查找目标实例。
        /// </summary>
        public string TargetInstanceId;

        /// <summary>
        /// 目标容器 ID。
        /// </summary>
        public string TargetContainerId;

        /// <summary>
        /// 目标格子索引。
        /// </summary>
        public int TargetSlotIndex = -1;

        /// <summary>
        /// 请求来源模块名。
        /// </summary>
        public string SourceModule;
    }
}
