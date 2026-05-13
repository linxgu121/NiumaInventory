using System;

namespace NiumaInventory.Request
{
    /// <summary>
    /// 使用物品请求。
    /// 背包只负责基础校验和数量扣减，具体业务效果由外部模块处理。
    /// </summary>
    [Serializable]
    public sealed class UseItemRequest
    {
        /// <summary>
        /// 要使用的物品实例 ID。
        /// </summary>
        public string InstanceId;

        /// <summary>
        /// 使用数量。
        /// 必须大于 0。
        /// </summary>
        public int Count = 1;

        /// <summary>
        /// 请求来源模块名。
        /// </summary>
        public string SourceModule;

        /// <summary>
        /// 使用上下文 ID。
        /// 例如目标对象 ID、技能 ID 或临时调试上下文。
        /// </summary>
        public string ContextId;
    }
}
