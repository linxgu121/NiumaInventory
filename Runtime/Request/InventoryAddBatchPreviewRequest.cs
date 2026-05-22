using System;

namespace NiumaInventory.Request
{
    /// <summary>
    /// 批量添加物品预检请求。
    /// 用于合成、多产物奖励等场景，在真正修改背包前模拟一整批 AddItem 是否可以连续成功。
    /// </summary>
    [Serializable]
    public sealed class InventoryAddBatchPreviewRequest
    {
        /// <summary>
        /// 要按顺序模拟添加的物品请求。
        /// 该数组中的请求不会被修改。
        /// </summary>
        public AddItemRequest[] Requests = Array.Empty<AddItemRequest>();

        /// <summary>
        /// 是否允许部分预检成功。
        /// false 时必须整批全部可放入，否则返回失败。
        /// </summary>
        public bool AllowPartial;

        /// <summary>
        /// 请求来源模块名。
        /// 仅用于日志、调试和排查链路，不作为业务判断主键。
        /// </summary>
        public string SourceModule;
    }
}
