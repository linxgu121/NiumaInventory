using NiumaInventory.Data;
using NiumaInventory.Request;

namespace NiumaInventory.Service
{
    /// <summary>
    /// 背包查询接口。
    /// 查询接口只读取数据，不修改背包状态。
    /// </summary>
    public interface IInventoryQuery
    {
        /// <summary>
        /// 查询整个背包是否拥有指定数量的物品。
        /// count 必须大于 0。
        /// </summary>
        bool HasItem(string itemId, int count);

        /// <summary>
        /// 查询整个背包中指定物品总数量。
        /// </summary>
        int GetItemCount(string itemId);

        /// <summary>
        /// 查询指定容器中指定物品数量。
        /// </summary>
        int GetItemCount(string itemId, string containerId);

        /// <summary>
        /// 尝试获取物品实例快照。
        /// 返回快照而不是内部运行时引用，避免外部直接修改背包内部状态。
        /// </summary>
        bool TryGetItem(string instanceId, out InventoryItemSnapshot item);

        /// <summary>
        /// 尝试获取容器快照。
        /// </summary>
        bool TryGetContainerSnapshot(string containerId, out InventoryContainerSnapshot container);

        /// <summary>
        /// 尝试查找指定容器中的第一个空格。
        /// 该查询不导出完整背包快照，适合装备、交互等模块在操作前做轻量定位。
        /// </summary>
        bool TryFindFirstEmptySlot(string containerId, out int slotIndex);

        /// <summary>
        /// 校验是否可以添加物品。
        /// 只做校验，不修改背包数据。
        /// </summary>
        InventoryOperationResult CanAddItem(AddItemRequest request);

        /// <summary>
        /// 校验是否可以移除物品。
        /// 只做校验，不修改背包数据。
        /// </summary>
        InventoryOperationResult CanRemoveItem(RemoveItemRequest request);
    }
}
