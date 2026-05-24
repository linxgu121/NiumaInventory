using System;
using System.Collections.Generic;
using NiumaCore.Module;
using NiumaInventory.Config;
using NiumaInventory.Data;
using NiumaInventory.Enum;
using NiumaInventory.Request;
using NiumaInventory.Service;
using UnityEngine;

namespace NiumaInventory.Controller
{
    /// <summary>
    /// NiumaInventory 背包模块根控制器。
    /// 负责把纯 C# 的 InventoryService 接入 Unity 生命周期、Inspector 配置和调试入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaInventoryController : MonoBehaviour, IGameModule
    {
        [Header("背包配置")]
        [Tooltip("物品静态定义列表。请拖入当前版本可用的所有 ItemDefinition。")]
        [SerializeField] private ItemDefinition[] itemDefinitions = Array.Empty<ItemDefinition>();

        [Tooltip("背包容器配置列表。至少需要一个主背包容器，否则无法添加物品。")]
        [SerializeField] private InventoryContainerConfig[] containerConfigs = Array.Empty<InventoryContainerConfig>();

        [Header("模块启动")]
        [Tooltip("Awake 时是否自动初始化背包服务。没有统一模块启动器时建议开启。")]
        [SerializeField] private bool initializeOnAwake = true;

        [Tooltip("OnEnable 时是否自动启动背包模块。没有统一模块启动器时建议开启。")]
        [SerializeField] private bool startOnEnable = true;

        [Tooltip("初始化时是否把 IInventoryService、IInventoryQuery、IInventoryCommand 注册到 GameContext。使用统一 GameContext 的项目建议开启。")]
        [SerializeField] private bool registerServiceToContext = true;

        [Header("调试：物品")]
        [Tooltip("调试用物品 ID。右键组件菜单可以用它添加、移除或查询物品。")]
        [SerializeField] private string debugItemId;

        [Tooltip("调试用数量。添加、移除、拆分或使用物品时读取该值。")]
        [SerializeField] private int debugCount = 1;

        [Tooltip("调试用物品实例 ID。移动、拆分、合并、使用、锁定、解锁时读取该值。")]
        [SerializeField] private string debugInstanceId;

        [Tooltip("调试添加或移除时限定的容器 ID。为空时由背包服务按规则选择或从全背包扣减。")]
        [SerializeField] private string debugContainerId;

        [Tooltip("调试添加时的目标格子。小于 0 表示自动寻找格子。")]
        [SerializeField] private int debugSlotIndex = -1;

        [Tooltip("调试添加时是否允许部分成功。开启后放不下的数量会进入 OverflowItems。")]
        [SerializeField] private bool debugAllowPartial;

        [Header("调试：移动/合并/排序")]
        [Tooltip("调试移动或拆分时的目标容器 ID。")]
        [SerializeField] private string debugTargetContainerId;

        [Tooltip("调试移动或拆分时的目标格子。")]
        [SerializeField] private int debugTargetSlotIndex = -1;

        [Tooltip("调试合并时的目标实例 ID。为空时按目标容器和目标格子查找。")]
        [SerializeField] private string debugTargetInstanceId;

        [Tooltip("调试移动时，如果目标格子被不同物品占用，是否允许交换。")]
        [SerializeField] private bool debugSwapIfOccupied;

        [Tooltip("调试整理容器时使用的排序维度。为空时由服务使用默认排序。")]
        [SerializeField] private InventorySortKey[] debugSortKeys = Array.Empty<InventorySortKey>();

        [Tooltip("调试整理容器时，是否保持锁定物品原格子。")]
        [SerializeField] private bool debugKeepLockedSlot = true;

        private InventoryService _inventoryService;
        private GameContext _context;
        private bool _warnedMissingItemDefinitions;
        private bool _warnedMissingContainerConfigs;

        /// <summary>
        /// 模块名称。
        /// </summary>
        public string ModuleName => "NiumaInventory";

        /// <summary>
        /// 背包服务门面接口。
        /// 该属性只返回当前服务引用，不触发懒初始化；需要兜底初始化时请使用控制器代理方法。
        /// </summary>
        public IInventoryService InventoryService => _inventoryService;

        /// <summary>
        /// 背包查询接口。
        /// UI、任务、剧情等只需要读背包状态的模块优先依赖该接口。
        /// </summary>
        public IInventoryQuery InventoryQuery => _inventoryService;

        /// <summary>
        /// 背包命令接口。
        /// 拾取、奖励、商城、合成等需要修改背包的模块依赖该接口。
        /// </summary>
        public IInventoryCommand InventoryCommand => _inventoryService;

        /// <summary>
        /// 当前模块是否已经初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 当前模块是否正在运行。
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 背包数据修订号。
        /// UI、存档或调试桥接层可通过该值判断是否需要重新拉取快照。
        /// </summary>
        public int InventoryRevision => _inventoryService != null ? _inventoryService.Revision : 0;

        /// <summary>
        /// 当前物品静态定义配置。
        /// UI 桥接层可只读使用，正式修改请通过 SetItemDefinitions。
        /// </summary>
        public ItemDefinition[] ItemDefinitions => itemDefinitions ?? Array.Empty<ItemDefinition>();

        /// <summary>
        /// 当前容器静态配置。
        /// UI 桥接层可只读使用，正式修改请通过 SetContainerConfigs。
        /// </summary>
        public InventoryContainerConfig[] ContainerConfigs => containerConfigs ?? Array.Empty<InventoryContainerConfig>();

        /// <summary>
        /// 最近一次调试或代理操作结果。
        /// </summary>
        public InventoryOperationResult LastOperationResult { get; private set; }

        private void Awake()
        {
            if (initializeOnAwake && !IsInitialized)
            {
                Initialize(null);
            }
        }

        private void OnEnable()
        {
            if (startOnEnable && IsInitialized && !IsRunning)
            {
                StartModule();
            }
        }

        private void OnDisable()
        {
            if (IsRunning)
            {
                StopModule();
            }
        }

        /// <summary>
        /// 初始化背包模块。
        /// 如果已有服务，会导出非临时快照并在新服务中恢复，避免重复初始化丢失正式背包数据。
        /// </summary>
        public void Initialize(GameContext context)
        {
            var wasRunning = IsRunning;
            var previousService = _inventoryService;
            var previousContext = _context;
            var previousInitialized = IsInitialized;
            var targetContext = context ?? _context;
            var previousRegisteredService = targetContext != null ? targetContext.GetService<IInventoryService>() : null;
            var previousRegisteredQuery = targetContext != null ? targetContext.GetService<IInventoryQuery>() : null;
            var previousRegisteredCommand = targetContext != null ? targetContext.GetService<IInventoryCommand>() : null;
            var initializedSuccessfully = false;
            IsRunning = false;

            try
            {
                _context = targetContext;
                WarnIfConfigMissing();

                var snapshot = previousService != null ? previousService.ExportSnapshot() : null;
                var newService = new InventoryService(itemDefinitions, containerConfigs);
                if (snapshot != null)
                {
                    newService.ImportSnapshot(snapshot);
                }

                _inventoryService = newService;
                RegisterServicesToContext();
                IsInitialized = true;
                initializedSuccessfully = true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NiumaInventory] 初始化背包模块失败：{exception.Message}", this);
                RestoreRegisteredInventoryServices(targetContext, previousRegisteredService, previousRegisteredQuery, previousRegisteredCommand);
                _inventoryService = previousService;
                _context = previousContext;
                IsInitialized = previousInitialized;
            }
            finally
            {
                IsRunning = initializedSuccessfully
                    ? wasRunning && _inventoryService != null
                    : wasRunning && previousInitialized && previousService != null;
            }
        }

        /// <summary>
        /// 启动背包模块。
        /// </summary>
        public void StartModule()
        {
            if (!IsInitialized)
            {
                Initialize(_context);
            }

            IsRunning = true;
        }

        /// <summary>
        /// 停止背包模块。
        /// 这里只关闭运行标记，不导出存档；存档由 NiumaSave 或上层流程统一触发。
        /// </summary>
        public void StopModule()
        {
            IsRunning = false;
        }

        /// <summary>
        /// 背包模块帧更新。
        /// 当前背包核心服务是请求驱动，MVP 阶段不需要每帧逻辑。
        /// </summary>
        public void Tick(float deltaTime)
        {
        }

        /// <summary>
        /// 运行时替换物品定义。
        /// 会刷新 MissingItem 标记和重量缓存。
        /// </summary>
        public void SetItemDefinitions(ItemDefinition[] definitions)
        {
            itemDefinitions = definitions ?? Array.Empty<ItemDefinition>();
            if (_inventoryService != null)
            {
                _inventoryService.SetItemDefinitions(itemDefinitions);
            }
        }

        /// <summary>
        /// 运行时替换容器配置。
        /// 当前正式背包数据会保留，但容器规则和重量缓存会刷新。
        /// </summary>
        public void SetContainerConfigs(InventoryContainerConfig[] configs)
        {
            containerConfigs = configs ?? Array.Empty<InventoryContainerConfig>();
            if (_inventoryService != null)
            {
                _inventoryService.SetContainerConfigs(containerConfigs);
            }
        }

        public InventoryOperationResult AddItem(AddItemRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.AddItem(request));
        }

        public InventoryOperationResult RemoveItem(RemoveItemRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.RemoveItem(request));
        }

        /// <summary>
        /// 校验当前请求是否可以添加物品，只检查规则，不修改背包数据。
        /// </summary>
        public InventoryOperationResult CanAddItem(AddItemRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.CanAddItem(request));
        }

        /// <summary>
        /// 校验一批添加请求是否可以按顺序连续放入背包，只检查规则，不修改背包数据。
        /// </summary>
        public InventoryOperationResult CanAddItemsBatch(InventoryAddBatchPreviewRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.CanAddItemsBatch(request));
        }

        /// <summary>
        /// 校验当前请求是否可以移除物品，只检查规则，不修改背包数据。
        /// </summary>
        public InventoryOperationResult CanRemoveItem(RemoveItemRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.CanRemoveItem(request));
        }

        public InventoryOperationResult MoveItem(MoveItemRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.MoveItem(request));
        }

        public InventoryOperationResult SplitStack(SplitStackRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.SplitStack(request));
        }

        public InventoryOperationResult MergeStack(MergeStackRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.MergeStack(request));
        }

        public InventoryOperationResult SortContainer(SortContainerRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.SortContainer(request));
        }

        public InventoryOperationResult UseItem(UseItemRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.UseItem(request));
        }

        public InventoryOperationResult LockItem(string instanceId)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.LockItem(instanceId));
        }

        public InventoryOperationResult UnlockItem(string instanceId)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "背包服务未初始化。"));
            }

            return StoreResult(_inventoryService.UnlockItem(instanceId));
        }

        public bool HasItem(string itemId, int count)
        {
            return EnsureServiceReady() && _inventoryService.HasItem(itemId, count);
        }

        public int GetItemCount(string itemId)
        {
            return EnsureServiceReady() ? _inventoryService.GetItemCount(itemId) : 0;
        }

        public int GetItemCount(string itemId, string containerId)
        {
            return EnsureServiceReady() ? _inventoryService.GetItemCount(itemId, containerId) : 0;
        }

        public bool TryGetItem(string instanceId, out InventoryItemSnapshot item)
        {
            item = null;
            return EnsureServiceReady() && _inventoryService.TryGetItem(instanceId, out item);
        }

        public bool TryGetContainerSnapshot(string containerId, out InventoryContainerSnapshot container)
        {
            container = null;
            return EnsureServiceReady() && _inventoryService.TryGetContainerSnapshot(containerId, out container);
        }

        /// <summary>
        /// 复制容器快照到调用方缓存列表。
        /// UI 桥接层优先使用该轻量查询，不要为了刷新界面调用 ExportSnapshot。
        /// </summary>
        public void CopyContainerSnapshots(List<InventoryContainerSnapshot> output)
        {
            if (!EnsureServiceReady())
            {
                output?.Clear();
                return;
            }

            _inventoryService.CopyContainerSnapshots(output);
        }

        /// <summary>
        /// 复制物品快照到调用方缓存列表。
        /// 该方法只读背包状态，不创建完整存档对象。
        /// </summary>
        public void CopyItemSnapshots(List<InventoryItemSnapshot> output)
        {
            if (!EnsureServiceReady())
            {
                output?.Clear();
                return;
            }

            _inventoryService.CopyItemSnapshots(output);
        }

        public bool TryFindFirstEmptySlot(string containerId, out int slotIndex)
        {
            slotIndex = -1;
            return EnsureServiceReady() && _inventoryService.TryFindFirstEmptySlot(containerId, out slotIndex);
        }

        /// <summary>
        /// 按物品 ID 查找静态定义。
        /// UI 桥接层用它补齐显示名称、图标、品质和基础规则。
        /// </summary>
        public bool TryGetItemDefinition(string itemId, out ItemDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(itemId) || itemDefinitions == null)
            {
                return false;
            }

            for (var i = 0; i < itemDefinitions.Length; i++)
            {
                var candidate = itemDefinitions[i];
                if (candidate != null && string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 按容器 ID 查找静态配置。
        /// UI 桥接层用它补齐容器显示名称和类型信息。
        /// </summary>
        public bool TryGetContainerConfig(string containerId, out InventoryContainerConfig config)
        {
            config = null;
            if (string.IsNullOrWhiteSpace(containerId) || containerConfigs == null)
            {
                return false;
            }

            for (var i = 0; i < containerConfigs.Length; i++)
            {
                var candidate = containerConfigs[i];
                if (candidate != null && string.Equals(candidate.ContainerId, containerId, StringComparison.Ordinal))
                {
                    config = candidate;
                    return true;
                }
            }

            return false;
        }

        public InventorySaveData ExportSnapshot()
        {
            return EnsureServiceReady() ? _inventoryService.ExportSnapshot() : new InventorySaveData();
        }

        public void ImportSnapshot(InventorySaveData snapshot)
        {
            if (!EnsureServiceReady())
            {
                return;
            }

            if (snapshot == null)
            {
                Debug.LogWarning("[NiumaInventory] ImportSnapshot 收到 null，已拒绝导入，避免误清空背包。", this);
                return;
            }

            _inventoryService.ImportSnapshot(snapshot);
        }

        [ContextMenu("NiumaInventory/重新初始化服务")]
        private void DebugReinitialize()
        {
            Initialize(_context);
            Debug.Log("[NiumaInventory] 背包服务已重新初始化。", this);
        }

        [ContextMenu("NiumaInventory/添加调试物品")]
        private void DebugAddItem()
        {
            var result = AddItem(new AddItemRequest
            {
                ItemId = debugItemId,
                Count = debugCount,
                TargetContainerId = debugContainerId,
                TargetSlotIndex = debugSlotIndex,
                AllowPartial = debugAllowPartial,
                SourceModule = nameof(NiumaInventoryController)
            });
            LogResult("添加物品", result);
        }

        [ContextMenu("NiumaInventory/移除调试物品")]
        private void DebugRemoveItem()
        {
            var result = RemoveItem(new RemoveItemRequest
            {
                ItemId = debugItemId,
                InstanceId = debugInstanceId,
                Count = debugCount,
                ContainerId = debugContainerId,
                SourceModule = nameof(NiumaInventoryController)
            });
            LogResult("移除物品", result);
        }

        [ContextMenu("NiumaInventory/移动调试物品")]
        private void DebugMoveItem()
        {
            var result = MoveItem(new MoveItemRequest
            {
                InstanceId = debugInstanceId,
                TargetContainerId = debugTargetContainerId,
                TargetSlotIndex = debugTargetSlotIndex,
                SwapIfOccupied = debugSwapIfOccupied,
                SourceModule = nameof(NiumaInventoryController)
            });
            LogResult("移动物品", result);
        }

        [ContextMenu("NiumaInventory/拆分调试物品")]
        private void DebugSplitStack()
        {
            var result = SplitStack(new SplitStackRequest
            {
                SourceInstanceId = debugInstanceId,
                SplitCount = debugCount,
                TargetContainerId = debugTargetContainerId,
                TargetSlotIndex = debugTargetSlotIndex,
                SourceModule = nameof(NiumaInventoryController)
            });
            LogResult("拆分物品", result);
        }

        [ContextMenu("NiumaInventory/合并调试物品")]
        private void DebugMergeStack()
        {
            var result = MergeStack(new MergeStackRequest
            {
                SourceInstanceId = debugInstanceId,
                TargetInstanceId = debugTargetInstanceId,
                TargetContainerId = debugTargetContainerId,
                TargetSlotIndex = debugTargetSlotIndex,
                SourceModule = nameof(NiumaInventoryController)
            });
            LogResult("合并物品", result);
        }

        [ContextMenu("NiumaInventory/整理调试容器")]
        private void DebugSortContainer()
        {
            var containerId = !string.IsNullOrWhiteSpace(debugTargetContainerId)
                ? debugTargetContainerId
                : debugContainerId;
            var result = SortContainer(new SortContainerRequest
            {
                ContainerId = containerId,
                SortKeys = debugSortKeys,
                KeepLockedSlot = debugKeepLockedSlot,
                SourceModule = nameof(NiumaInventoryController)
            });
            LogResult("整理容器", result);
        }

        [ContextMenu("NiumaInventory/使用调试物品")]
        private void DebugUseItem()
        {
            var result = UseItem(new UseItemRequest
            {
                InstanceId = debugInstanceId,
                Count = debugCount,
                SourceModule = nameof(NiumaInventoryController),
                ContextId = "debug"
            });
            LogResult("使用物品", result);
        }

        [ContextMenu("NiumaInventory/锁定调试物品")]
        private void DebugLockItem()
        {
            LogResult("锁定物品", LockItem(debugInstanceId));
        }

        [ContextMenu("NiumaInventory/解锁调试物品")]
        private void DebugUnlockItem()
        {
            LogResult("解锁物品", UnlockItem(debugInstanceId));
        }

        [ContextMenu("NiumaInventory/打印调试物品快照")]
        private void DebugPrintItemSnapshot()
        {
            if (!TryGetItem(debugInstanceId, out var item) || item == null)
            {
                Debug.LogWarning($"[NiumaInventory] 未找到物品实例：{debugInstanceId}", this);
                return;
            }

            Debug.Log($"[NiumaInventory] 物品快照 InstanceId={item.InstanceId}, ItemId={item.ItemId}, Count={item.Count}, Container={item.ContainerId}, Slot={item.SlotIndex}, Locked={item.IsLocked}, Missing={item.IsMissing}, Order={item.AcquiredOrder}", this);
        }

        [ContextMenu("NiumaInventory/打印调试容器快照")]
        private void DebugPrintContainerSnapshot()
        {
            var containerId = !string.IsNullOrWhiteSpace(debugTargetContainerId)
                ? debugTargetContainerId
                : debugContainerId;
            if (!TryGetContainerSnapshot(containerId, out var container) || container == null)
            {
                Debug.LogWarning($"[NiumaInventory] 未找到容器：{containerId}", this);
                return;
            }

            Debug.Log($"[NiumaInventory] 容器快照 ContainerId={container.ContainerId}, Type={container.ContainerType}, SlotCount={container.SlotCount}, Weight={container.CurrentWeight}/{container.MaxWeight}, Unlocked={container.IsUnlocked}", this);
        }

        [ContextMenu("NiumaInventory/打印背包存档快照")]
        private void DebugPrintSaveSnapshot()
        {
            var snapshot = ExportSnapshot();
            Debug.Log($"[NiumaInventory] 背包快照 Revision={snapshot.Revision}, Containers={snapshot.Containers?.Length ?? 0}, Items={snapshot.Items?.Length ?? 0}", this);
        }

        private bool EnsureServiceReady()
        {
            if (!IsInitialized || _inventoryService == null)
            {
                Initialize(_context);
            }

            return _inventoryService != null;
        }

        private void RegisterServicesToContext()
        {
            if (_context == null)
            {
                return;
            }

            if (!registerServiceToContext)
            {
                return;
            }

            if (_inventoryService == null)
            {
                Debug.LogWarning("[NiumaInventory] 背包服务为空，已跳过 GameContext 注册，避免清除其它启动器注册的服务。", this);
                return;
            }

            _context.RegisterService<IInventoryService>(_inventoryService);
            _context.RegisterService<IInventoryQuery>(_inventoryService);
            _context.RegisterService<IInventoryCommand>(_inventoryService);
        }

        private void RestoreRegisteredInventoryServices(GameContext context, IInventoryService service, IInventoryQuery query, IInventoryCommand command)
        {
            if (context == null || !registerServiceToContext)
            {
                return;
            }

            // 初始化中途失败时恢复进入 Initialize 前的注册状态，避免 GameContext 指向已丢弃的新服务。
            context.RegisterService<IInventoryService>(service);
            context.RegisterService<IInventoryQuery>(query);
            context.RegisterService<IInventoryCommand>(command);
        }

        private void WarnIfConfigMissing()
        {
            if ((itemDefinitions == null || itemDefinitions.Length == 0) && !_warnedMissingItemDefinitions)
            {
                Debug.LogWarning("[NiumaInventory] 未配置任何 ItemDefinition。背包服务可以运行，但添加正式物品会失败或读档物品会进入 MissingItem 保护。", this);
                _warnedMissingItemDefinitions = true;
            }

            if ((containerConfigs == null || containerConfigs.Length == 0) && !_warnedMissingContainerConfigs)
            {
                Debug.LogWarning("[NiumaInventory] 未配置任何 InventoryContainerConfig。背包服务可以创建，但无法放入物品。", this);
                _warnedMissingContainerConfigs = true;
            }
        }

        private InventoryOperationResult StoreResult(InventoryOperationResult result)
        {
            LastOperationResult = result;
            return result;
        }

        private void LogResult(string actionName, InventoryOperationResult result)
        {
            LastOperationResult = result;
            if (result == null)
            {
                Debug.LogWarning($"[NiumaInventory] {actionName} 返回空结果。", this);
                return;
            }

            Debug.Log($"[NiumaInventory] {actionName}：Succeeded={result.Succeeded}, Reason={result.Reason}, Message={result.Message}, Added={result.AddedItems?.Length ?? 0}, Removed={result.RemovedItems?.Length ?? 0}, Changed={result.ChangedItems?.Length ?? 0}, Overflow={result.OverflowItems?.Length ?? 0}, Revision={InventoryRevision}", this);
        }
    }
}
