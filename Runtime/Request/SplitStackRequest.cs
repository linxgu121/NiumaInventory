using System;

namespace NiumaInventory.Request
{
    /// <summary>
    /// 拆分堆叠请求。
    /// 拆分成功后新堆必须生成新的 InstanceId。
    /// </summary>
    [Serializable]
    public sealed class SplitStackRequest
    {
        /// <summary>
        /// 源物品实例 ID。
        /// </summary>
        public string SourceInstanceId;

        /// <summary>
        /// 拆分数量。
        /// 必须大于 0 且小于源堆数量。
        /// </summary>
        public int SplitCount = 1;

        /// <summary>
        /// 目标容器 ID。
        /// 为空时默认使用源容器。
        /// </summary>
        public string TargetContainerId;

        /// <summary>
        /// 目标格子索引。
        /// 小于 0 表示自动寻找空格或可合并格。
        /// </summary>
        public int TargetSlotIndex = -1;

        /// <summary>
        /// 请求来源模块名。
        /// </summary>
        public string SourceModule;
    }
}
