using NiumaInventory.Data;

namespace NiumaInventory.Service
{
    /// <summary>
    /// 背包服务门面接口。
    /// 第一版作为查询、命令和持久化的统一入口；后续接口膨胀时可再拆能力接口。
    /// </summary>
    public interface IInventoryService : IInventoryQuery, IInventoryCommand
    {
        /// <summary>
        /// 背包全局修订号。
        /// 任意会改变背包事实的操作成功后都应递增。
        /// </summary>
        int Revision { get; }

        /// <summary>
        /// 显式导出背包存档快照。
        /// 不允许外部直接序列化运行时对象。
        /// </summary>
        InventorySaveData ExportSnapshot();

        /// <summary>
        /// 从存档快照恢复背包状态。
        /// 读档后应继承快照中的 Revision，并在需要迁移或修复时再递增。
        /// </summary>
        void ImportSnapshot(InventorySaveData snapshot);
    }
}
