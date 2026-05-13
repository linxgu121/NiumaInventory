using System;

namespace NiumaInventory.Request
{
    /// <summary>
    /// 移动物品请求。
    /// 用于拖拽、整理、装备模块移动实例等场景。
    /// </summary>
    [Serializable]
    public sealed class MoveItemRequest
    {
        /// <summary>
        /// 要移动的物品实例 ID。
        /// </summary>
        public string InstanceId;

        /// <summary>
        /// 目标容器 ID。
        /// </summary>
        public string TargetContainerId;

        /// <summary>
        /// 目标格子索引。
        /// </summary>
        public int TargetSlotIndex = -1;

        /// <summary>
        /// 目标格子已占用时是否允许交换。
        /// 合并堆叠不依赖该字段，由背包服务根据物品规则判断。
        /// </summary>
        public bool SwapIfOccupied;

        /// <summary>
        /// 请求来源模块名。
        /// </summary>
        public string SourceModule;
    }
}
