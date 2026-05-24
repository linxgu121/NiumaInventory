using System.Collections.Generic;
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
        /// 复制当前容器快照到调用方提供的缓存列表。
        /// 该接口用于 UI、调试面板等只读展示场景，避免为了刷新界面创建完整 InventorySaveData。
        /// </summary>
        void CopyContainerSnapshots(List<InventoryContainerSnapshot> output);

        /// <summary>
        /// 复制当前物品快照到调用方提供的缓存列表。
        /// 调用方不要长期持有返回对象作为存档事实，正式存档仍应走 ExportSnapshot。
        /// </summary>
        void CopyItemSnapshots(List<InventoryItemSnapshot> output);

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
        /// 校验一批 AddItem 请求是否可以按顺序连续放入背包。
        /// 只做预检，不修改背包数据；用于合成、多产物奖励等需要整批成功或整批失败的场景。
        /// </summary>
        InventoryOperationResult CanAddItemsBatch(InventoryAddBatchPreviewRequest request);

        /// <summary>
        /// 校验是否可以移除物品。
        /// 只做校验，不修改背包数据。
        /// </summary>
        InventoryOperationResult CanRemoveItem(RemoveItemRequest request);
    }
}
