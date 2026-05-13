using System;
using NiumaInventory.Enum;

namespace NiumaInventory.Data
{
    /// <summary>
    /// 背包操作结果。
    /// 所有修改操作都应返回结构化结果，避免调用方通过字符串判断失败原因。
    /// </summary>
    [Serializable]
    public sealed class InventoryOperationResult
    {
        /// <summary>
        /// 操作是否成功。
        /// 部分成功时该值也可以为 true，但 OverflowItems 不为空。
        /// </summary>
        public bool Succeeded;

        /// <summary>
        /// 失败原因。
        /// 成功时为 None。
        /// </summary>
        public InventoryFailureReason Reason;

        /// <summary>
        /// 调试信息或临时提示。
        /// 正式本地化不要依赖该字段。
        /// </summary>
        public string Message;

        /// <summary>
        /// 本次操作新增或生成的物品快照。
        /// </summary>
        public InventoryItemSnapshot[] AddedItems = Array.Empty<InventoryItemSnapshot>();

        /// <summary>
        /// 本次操作移除的物品快照。
        /// </summary>
        public InventoryItemSnapshot[] RemovedItems = Array.Empty<InventoryItemSnapshot>();

        /// <summary>
        /// 本次操作发生变化的物品快照。
        /// </summary>
        public InventoryItemSnapshot[] ChangedItems = Array.Empty<InventoryItemSnapshot>();

        /// <summary>
        /// 部分成功时未能放入背包的剩余物品。
        /// 只有请求明确允许部分成功时才应该使用。
        /// </summary>
        public InventoryItemSnapshot[] OverflowItems = Array.Empty<InventoryItemSnapshot>();

        public static InventoryOperationResult Success(
            InventoryItemSnapshot[] addedItems = null,
            InventoryItemSnapshot[] removedItems = null,
            InventoryItemSnapshot[] changedItems = null,
            InventoryItemSnapshot[] overflowItems = null,
            string message = null)
        {
            return new InventoryOperationResult
            {
                Succeeded = true,
                Reason = InventoryFailureReason.None,
                Message = message,
                AddedItems = addedItems ?? Array.Empty<InventoryItemSnapshot>(),
                RemovedItems = removedItems ?? Array.Empty<InventoryItemSnapshot>(),
                ChangedItems = changedItems ?? Array.Empty<InventoryItemSnapshot>(),
                OverflowItems = overflowItems ?? Array.Empty<InventoryItemSnapshot>()
            };
        }

        public static InventoryOperationResult Failed(InventoryFailureReason reason, string message = null)
        {
            return new InventoryOperationResult
            {
                Succeeded = false,
                Reason = reason,
                Message = message,
                AddedItems = Array.Empty<InventoryItemSnapshot>(),
                RemovedItems = Array.Empty<InventoryItemSnapshot>(),
                ChangedItems = Array.Empty<InventoryItemSnapshot>(),
                OverflowItems = Array.Empty<InventoryItemSnapshot>()
            };
        }
    }
}
