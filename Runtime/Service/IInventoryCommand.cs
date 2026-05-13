using NiumaInventory.Data;
using NiumaInventory.Request;

namespace NiumaInventory.Service
{
    /// <summary>
    /// 背包命令接口。
    /// 所有方法都可能修改背包状态，成功后应由实现层递增 Revision。
    /// </summary>
    public interface IInventoryCommand
    {
        /// <summary>
        /// 添加物品。
        /// 默认整批成功或整批失败，只有请求允许部分成功时才返回 OverflowItems。
        /// </summary>
        InventoryOperationResult AddItem(AddItemRequest request);

        /// <summary>
        /// 移除物品。
        /// </summary>
        InventoryOperationResult RemoveItem(RemoveItemRequest request);

        /// <summary>
        /// 移动物品实例。
        /// </summary>
        InventoryOperationResult MoveItem(MoveItemRequest request);

        /// <summary>
        /// 拆分堆叠。
        /// </summary>
        InventoryOperationResult SplitStack(SplitStackRequest request);

        /// <summary>
        /// 合并堆叠。
        /// </summary>
        InventoryOperationResult MergeStack(MergeStackRequest request);

        /// <summary>
        /// 整理容器。
        /// </summary>
        InventoryOperationResult SortContainer(SortContainerRequest request);

        /// <summary>
        /// 使用物品。
        /// 背包只处理基础校验和扣减，具体效果由外部模块处理。
        /// </summary>
        InventoryOperationResult UseItem(UseItemRequest request);

        /// <summary>
        /// 锁定物品实例。
        /// </summary>
        InventoryOperationResult LockItem(string instanceId);

        /// <summary>
        /// 解锁物品实例。
        /// </summary>
        InventoryOperationResult UnlockItem(string instanceId);
    }
}
