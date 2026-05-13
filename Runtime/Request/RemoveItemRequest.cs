using System;

namespace NiumaInventory.Request
{
    /// <summary>
    /// 移除物品请求。
    /// 可以按实例 ID 移除，也可以按 ItemId 和数量从指定或全部容器中扣减。
    /// </summary>
    [Serializable]
    public sealed class RemoveItemRequest
    {
        /// <summary>
        /// 要移除的物品 ID。
        /// InstanceId 为空时使用该字段按数量扣减。
        /// </summary>
        public string ItemId;

        /// <summary>
        /// 要移除的实例 ID。
        /// 如果填写，优先按实例定位。
        /// </summary>
        public string InstanceId;

        /// <summary>
        /// 要移除的数量。
        /// 必须大于 0。
        /// </summary>
        public int Count = 1;

        /// <summary>
        /// 限定扣减的容器 ID。
        /// 为空时可以从整个背包范围扣减。
        /// </summary>
        public string ContainerId;

        /// <summary>
        /// 请求来源模块名。
        /// </summary>
        public string SourceModule;
    }
}
