namespace NiumaInventory.Bridge
{
    /// <summary>
    /// 背包 UI 接收接口。
    /// 由具体 UI 组件实现，桥接层只负责把整理好的表现数据交给它，不直接操作按钮、格子或预制体。
    /// </summary>
    public interface IInventoryUIReceiver
    {
        /// <summary>
        /// 应用背包 UI 更新。
        /// update 中已经包含容器列表、物品列表、当前选中容器和当前选中物品。
        /// </summary>
        void ApplyInventoryUpdate(InventoryUIUpdate update);
    }
}
