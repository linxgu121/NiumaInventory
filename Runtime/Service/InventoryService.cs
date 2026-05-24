using System;
using System.Collections.Generic;
using NiumaInventory.Config;
using NiumaInventory.Data;
using NiumaInventory.Enum;
using NiumaInventory.Request;
using UnityEngine;

namespace NiumaInventory.Service
{
    /// <summary>
    /// 背包核心服务。
    /// 负责物品增删查改、堆叠、拆分、合并、容量重量校验和存档快照导入导出。
    /// </summary>
    public sealed class InventoryService : IInventoryService
    {
        private const string InstanceIdPrefix = "inv_";

        private readonly ItemDefinitionRegistry _definitionRegistry;
        private readonly Dictionary<string, InventoryContainerConfig> _containerConfigs =
            new Dictionary<string, InventoryContainerConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, InventoryContainerRuntime> _containers =
            new Dictionary<string, InventoryContainerRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, InventoryItemRuntime> _items =
            new Dictionary<string, InventoryItemRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _slotIndex =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _containerOrder = new List<string>();

        private int _revision;
        private long _nextAcquiredOrder = 1;

        /// <summary>
        /// 背包全局修订号。
        /// </summary>
        public int Revision => _revision;

        public InventoryService(
            IEnumerable<ItemDefinition> itemDefinitions = null,
            IEnumerable<InventoryContainerConfig> containerConfigs = null)
        {
            _definitionRegistry = new ItemDefinitionRegistry(itemDefinitions);
            SetContainerConfigs(containerConfigs);
        }

        /// <summary>
        /// 重建物品定义索引。
        /// 不会修改玩家已经持有的物品实例。
        /// </summary>
        public void SetItemDefinitions(IEnumerable<ItemDefinition> itemDefinitions)
        {
            _definitionRegistry.SetDefinitions(itemDefinitions);
            RefreshMissingFlags();
            RecalculateContainerWeights();
        }

        /// <summary>
        /// 重建容器配置和运行时容器。
        /// 第二阶段核心服务默认由初始化流程调用；运行中热替换配置时，调用方需要自行确认迁移策略。
        /// </summary>
        public void SetContainerConfigs(IEnumerable<InventoryContainerConfig> containerConfigs)
        {
            _containerConfigs.Clear();
            _containers.Clear();
            _slotIndex.Clear();
            _containerOrder.Clear();

            if (containerConfigs == null)
            {
                return;
            }

            foreach (var config in containerConfigs)
            {
                if (config == null || string.IsNullOrWhiteSpace(config.ContainerId))
                {
                    continue;
                }

                if (_containerConfigs.ContainsKey(config.ContainerId))
                {
                    Debug.LogWarning($"[NiumaInventory] 发现重复 ContainerId：{config.ContainerId}，后续重复容器配置已跳过。");
                    continue;
                }

                _containerConfigs[config.ContainerId] = config;
                _containerOrder.Add(config.ContainerId);
                _containers[config.ContainerId] = CreateContainerFromConfig(config);
            }

            RecalculateContainerWeights();
        }

        public bool HasItem(string itemId, int count)
        {
            if (count <= 0)
            {
                Debug.LogWarning($"[NiumaInventory] HasItem 收到无效数量：ItemId={itemId}, Count={count}。");
                return false;
            }

            return !string.IsNullOrWhiteSpace(itemId)
                   && GetItemCount(itemId) >= count;
        }

        public int GetItemCount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            var total = 0;
            foreach (var pair in _items)
            {
                var item = pair.Value;
                if (item != null && string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    total += Math.Max(0, item.Count);
                }
            }

            return total;
        }

        public int GetItemCount(string itemId, string containerId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(containerId))
            {
                return 0;
            }

            var total = 0;
            foreach (var pair in _items)
            {
                var item = pair.Value;
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.ItemId, itemId, StringComparison.Ordinal)
                    && string.Equals(item.ContainerId, containerId, StringComparison.Ordinal))
                {
                    total += Math.Max(0, item.Count);
                }
            }

            return total;
        }

        public bool TryGetItem(string instanceId, out InventoryItemSnapshot item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(instanceId)
                || !_items.TryGetValue(instanceId, out var runtime)
                || runtime == null)
            {
                return false;
            }

            item = runtime.ToSnapshot();
            return true;
        }

        public bool TryGetContainerSnapshot(string containerId, out InventoryContainerSnapshot container)
        {
            container = null;
            if (string.IsNullOrWhiteSpace(containerId)
                || !_containers.TryGetValue(containerId, out var runtime)
                || runtime == null)
            {
                return false;
            }

            container = runtime.ToSnapshot();
            return true;
        }

        public void CopyContainerSnapshots(List<InventoryContainerSnapshot> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            for (var i = 0; i < _containerOrder.Count; i++)
            {
                var containerId = _containerOrder[i];
                if (string.IsNullOrWhiteSpace(containerId)
                    || !_containers.TryGetValue(containerId, out var runtime)
                    || runtime == null)
                {
                    continue;
                }

                output.Add(runtime.ToSnapshot());
            }
        }

        public void CopyItemSnapshots(List<InventoryItemSnapshot> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            foreach (var pair in _items)
            {
                var runtime = pair.Value;
                if (runtime == null || string.IsNullOrWhiteSpace(runtime.InstanceId))
                {
                    continue;
                }

                output.Add(runtime.ToSnapshot());
            }
        }

        public bool TryFindFirstEmptySlot(string containerId, out int slotIndex)
        {
            slotIndex = FindFirstEmptySlot(containerId);
            return slotIndex >= 0;
        }

        public InventoryOperationResult CanAddItem(AddItemRequest request)
        {
            var plan = BuildAddPlan(request);
            return plan.CanCommit
                ? InventoryOperationResult.Success(message: "可以添加物品。")
                : InventoryOperationResult.Failed(plan.FailureReason, plan.FailureMessage);
        }

        public InventoryOperationResult CanAddItemsBatch(InventoryAddBatchPreviewRequest request)
        {
            if (request == null || request.Requests == null || request.Requests.Length == 0)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "批量添加预检请求为空。");
            }

            var context = new AddBatchPlanContext();
            var overflowItems = new List<InventoryItemSnapshot>();
            for (var i = 0; i < request.Requests.Length; i++)
            {
                var addRequest = CloneAddRequestForBatch(request.Requests[i], request.AllowPartial, request.SourceModule);
                var plan = BuildAddPlan(addRequest, context);
                if (!plan.CanCommit)
                {
                    return InventoryOperationResult.Failed(plan.FailureReason, $"第 {i + 1} 个添加请求预检失败：{plan.FailureMessage}");
                }

                context.Apply(addRequest, plan);
                if (plan.OverflowCount > 0)
                {
                    overflowItems.AddRange(CreateOverflowSnapshots(addRequest, plan.OverflowCount));
                }
            }

            return InventoryOperationResult.Success(
                overflowItems: overflowItems.ToArray(),
                message: overflowItems.Count > 0 ? "批量预检部分产物无法放入背包。" : "可以批量添加物品。");
        }

        public InventoryOperationResult CanRemoveItem(RemoveItemRequest request)
        {
            return ValidateRemoveRequest(request, out _);
        }

        public InventoryOperationResult AddItem(AddItemRequest request)
        {
            var plan = BuildAddPlan(request);
            if (!plan.CanCommit)
            {
                return InventoryOperationResult.Failed(plan.FailureReason, plan.FailureMessage);
            }

            var addedItems = new List<InventoryItemSnapshot>();
            var changedItems = new List<InventoryItemSnapshot>();

            for (var i = 0; i < plan.Placements.Count; i++)
            {
                var placement = plan.Placements[i];
                if (!string.IsNullOrWhiteSpace(placement.TargetInstanceId)
                    && _items.TryGetValue(placement.TargetInstanceId, out var existing))
                {
                    existing.Count += placement.Count;
                    changedItems.Add(existing.ToSnapshot());
                    continue;
                }

                var item = new InventoryItemRuntime
                {
                    InstanceId = GenerateUniqueInstanceId(),
                    ItemId = request.ItemId,
                    Count = placement.Count,
                    ContainerId = placement.ContainerId,
                    SlotIndex = placement.SlotIndex,
                    IsMissing = false,
                    AcquiredOrder = GetNextAcquiredOrder(),
                    CustomData = InventoryCustomDataEntry.CloneArray(request.CustomData)
                };

                _items[item.InstanceId] = item;
                addedItems.Add(item.ToSnapshot());
            }

            RecalculateContainerWeights();
            BumpRevisionIfChanged(addedItems.Count > 0 || changedItems.Count > 0);

            return InventoryOperationResult.Success(
                addedItems.ToArray(),
                null,
                changedItems.ToArray(),
                CreateOverflowSnapshots(request, plan.OverflowCount),
                plan.OverflowCount > 0 ? "部分物品未能放入背包。" : null);
        }

        public InventoryOperationResult RemoveItem(RemoveItemRequest request)
        {
            var validation = ValidateRemoveRequest(request, out var removeSteps);
            if (!validation.Succeeded)
            {
                return validation;
            }

            var removedItems = new List<InventoryItemSnapshot>();
            var changedItems = new List<InventoryItemSnapshot>();

            for (var i = 0; i < removeSteps.Count; i++)
            {
                var step = removeSteps[i];
                if (!_items.TryGetValue(step.InstanceId, out var item) || item == null)
                {
                    continue;
                }

                if (step.Count >= item.Count)
                {
                    removedItems.Add(item.ToSnapshot());
                    _items.Remove(item.InstanceId);
                }
                else
                {
                    item.Count -= step.Count;
                    changedItems.Add(item.ToSnapshot());
                }
            }

            RecalculateContainerWeights();
            BumpRevisionIfChanged(removedItems.Count > 0 || changedItems.Count > 0);

            return InventoryOperationResult.Success(null, removedItems.ToArray(), changedItems.ToArray());
        }

        public InventoryOperationResult MoveItem(MoveItemRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.InstanceId))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "移动请求或实例 ID 为空。");
            }

            if (!_items.TryGetValue(request.InstanceId, out var source) || source == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemNotFound, "找不到要移动的物品实例。");
            }

            if (!TryGetMovableDefinition(source, out var sourceDefinition, out var failed))
            {
                return failed;
            }

            if (!TryGetContainerPairForMove(source.ContainerId, request.TargetContainerId, sourceDefinition, out var sourceContainer, out var targetContainer, out failed))
            {
                return failed;
            }

            if (!IsValidSlot(targetContainer, request.TargetSlotIndex))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.SlotInvalid, "目标格子索引无效。");
            }

            var targetItem = FindItemAt(request.TargetContainerId, request.TargetSlotIndex);
            if (targetItem == source)
            {
                return InventoryOperationResult.Success(message: "物品已经在目标格子。");
            }

            if (targetItem == null)
            {
                return MoveToEmptySlot(source, sourceDefinition, targetContainer, request.TargetSlotIndex);
            }

            if (string.Equals(targetItem.ItemId, source.ItemId, StringComparison.Ordinal))
            {
                return MergeRuntimeStacks(source, targetItem);
            }

            if (!request.SwapIfOccupied)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.SlotOccupied, "目标格子已被占用。");
            }

            return SwapItems(source, targetItem, sourceContainer, targetContainer, sourceDefinition);
        }

        public InventoryOperationResult SplitStack(SplitStackRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourceInstanceId) || request.SplitCount <= 0)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "拆分请求无效。");
            }

            if (!_items.TryGetValue(request.SourceInstanceId, out var source) || source == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemNotFound, "找不到要拆分的物品实例。");
            }

            if (source.IsLocked)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemLocked, "锁定物品不能拆分。");
            }

            if (!_definitionRegistry.TryGetDefinition(source.ItemId, out var definition))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemDefinitionMissing, "找不到物品定义，不能拆分。");
            }

            var maxStack = GetEffectiveMaxStack(definition);
            if (maxStack <= 1 || request.SplitCount >= source.Count)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "拆分数量无效或物品不可堆叠。");
            }

            var targetContainerId = string.IsNullOrWhiteSpace(request.TargetContainerId)
                ? source.ContainerId
                : request.TargetContainerId;

            if (!TryGetContainerPairForMove(source.ContainerId, targetContainerId, definition, out _, out var targetContainer, out var failed))
            {
                return failed;
            }

            var targetSlot = request.TargetSlotIndex >= 0
                ? request.TargetSlotIndex
                : FindFirstEmptySlot(targetContainer.ContainerId);

            if (!IsValidSlot(targetContainer, targetSlot))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InventoryFull, "没有可用于拆分的新格子。");
            }

            var targetItem = FindItemAt(targetContainer.ContainerId, targetSlot);
            if (targetItem != null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.SlotOccupied, "拆分必须生成新实例，目标格子必须为空。");
            }

            if (!CanAddWeight(targetContainer, definition, request.SplitCount, source.ContainerId == targetContainer.ContainerId))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.WeightLimitExceeded, "目标容器重量不足。");
            }

            source.Count -= request.SplitCount;
            var newItem = new InventoryItemRuntime
            {
                InstanceId = GenerateUniqueInstanceId(),
                ItemId = source.ItemId,
                Count = request.SplitCount,
                ContainerId = targetContainer.ContainerId,
                SlotIndex = targetSlot,
                IsMissing = source.IsMissing,
                AcquiredOrder = GetNextAcquiredOrder(),
                CustomData = InventoryCustomDataEntry.CloneArray(source.CustomData)
            };
            _items[newItem.InstanceId] = newItem;

            RecalculateContainerWeights();
            BumpRevisionIfChanged(true);

            return InventoryOperationResult.Success(
                new[] { newItem.ToSnapshot() },
                null,
                new[] { source.ToSnapshot() });
        }

        public InventoryOperationResult MergeStack(MergeStackRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourceInstanceId))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "合并请求无效。");
            }

            if (!_items.TryGetValue(request.SourceInstanceId, out var source) || source == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemNotFound, "找不到源物品实例。");
            }

            var target = !string.IsNullOrWhiteSpace(request.TargetInstanceId)
                ? GetItemOrNull(request.TargetInstanceId)
                : FindItemAt(request.TargetContainerId, request.TargetSlotIndex);

            if (target == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemNotFound, "找不到目标物品实例。");
            }

            return MergeRuntimeStacks(source, target);
        }

        public InventoryOperationResult SortContainer(SortContainerRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ContainerId))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "整理请求无效。");
            }

            if (!_containers.TryGetValue(request.ContainerId, out var container) || container == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ContainerMissing, "找不到要整理的容器。");
            }

            var sortKeys = request.SortKeys;
            if (sortKeys == null || sortKeys.Length == 0)
            {
                sortKeys = new[] { InventorySortKey.ItemType, InventorySortKey.Quality, InventorySortKey.ItemId };
            }

            for (var i = 0; i < sortKeys.Length; i++)
            {
                if (sortKeys[i] == InventorySortKey.None)
                {
                    return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "排序维度不能为 None。");
                }
            }

            var movableItems = new List<InventoryItemRuntime>();
            var lockedSlots = new HashSet<int>();
            foreach (var pair in _items)
            {
                var item = pair.Value;
                if (item == null || !string.Equals(item.ContainerId, container.ContainerId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (item.IsLocked && request.KeepLockedSlot)
                {
                    lockedSlots.Add(item.SlotIndex);
                    continue;
                }

                movableItems.Add(item);
            }

            movableItems.Sort((left, right) => CompareItems(left, right, sortKeys));

            var availableSlots = new List<int>();
            for (var slot = 0; slot < container.SlotCount; slot++)
            {
                if (!lockedSlots.Contains(slot))
                {
                    availableSlots.Add(slot);
                }
            }

            var changedItems = new List<InventoryItemSnapshot>();
            for (var i = 0; i < movableItems.Count && i < availableSlots.Count; i++)
            {
                var item = movableItems[i];
                var newSlot = availableSlots[i];
                if (item.SlotIndex == newSlot)
                {
                    continue;
                }

                item.SlotIndex = newSlot;
                changedItems.Add(item.ToSnapshot());
            }

            RebuildSlotIndex();
            BumpRevisionIfChanged(changedItems.Count > 0);
            return InventoryOperationResult.Success(null, null, changedItems.ToArray());
        }

        public InventoryOperationResult UseItem(UseItemRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.InstanceId) || request.Count <= 0)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "使用物品请求无效。");
            }

            if (!_items.TryGetValue(request.InstanceId, out var item) || item == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemNotFound, "找不到要使用的物品。");
            }

            if (item.IsLocked)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemLocked, "锁定物品不能使用。");
            }

            if (item.IsMissing)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemDefinitionMissing, "缺失物品定义的物品不能使用。");
            }

            if (!_definitionRegistry.TryGetDefinition(item.ItemId, out var definition))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemDefinitionMissing, "找不到物品定义，不能使用。");
            }

            if (!definition.CanUse)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemCannotUse, "该物品不允许使用。");
            }

            return RemoveItem(new RemoveItemRequest
            {
                InstanceId = request.InstanceId,
                Count = request.Count,
                SourceModule = request.SourceModule
            });
        }

        public InventoryOperationResult LockItem(string instanceId)
        {
            return SetLockState(instanceId, true);
        }

        public InventoryOperationResult UnlockItem(string instanceId)
        {
            return SetLockState(instanceId, false);
        }

        public InventorySaveData ExportSnapshot()
        {
            var containers = new List<InventoryContainerSnapshot>(_containers.Count);
            for (var i = 0; i < _containerOrder.Count; i++)
            {
                if (_containers.TryGetValue(_containerOrder[i], out var container) && container != null)
                {
                    if (IsTemporaryContainer(container.ContainerId))
                    {
                        continue;
                    }

                    containers.Add(container.ToSnapshot());
                }
            }

            var items = new List<InventoryItemRuntime>(_items.Count);
            foreach (var item in _items.Values)
            {
                if (item == null || IsTemporaryContainer(item.ContainerId))
                {
                    continue;
                }

                items.Add(item);
            }

            items.Sort((left, right) =>
            {
                var containerCompare = string.Compare(left?.ContainerId, right?.ContainerId, StringComparison.Ordinal);
                if (containerCompare != 0)
                {
                    return containerCompare;
                }

                var slotCompare = (left?.SlotIndex ?? -1).CompareTo(right?.SlotIndex ?? -1);
                return slotCompare != 0
                    ? slotCompare
                    : string.Compare(left?.InstanceId, right?.InstanceId, StringComparison.Ordinal);
            });

            var itemSnapshots = new List<InventoryItemSnapshot>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    itemSnapshots.Add(items[i].ToSnapshot());
                }
            }

            return new InventorySaveData
            {
                Version = 1,
                Revision = _revision,
                Containers = containers.ToArray(),
                Items = itemSnapshots.ToArray()
            };
        }

        public void ImportSnapshot(InventorySaveData snapshot)
        {
            _items.Clear();
            _containers.Clear();
            _slotIndex.Clear();
            _containerOrder.Clear();
            _nextAcquiredOrder = 1;

            if (snapshot == null)
            {
                _revision = 0;
                RebuildContainersFromConfig();
                return;
            }

            _revision = Math.Max(0, snapshot.Revision);
            ImportContainers(snapshot.Containers);
            ImportItems(snapshot.Items, out var repaired);
            RebuildMissingConfiguredContainers();
            RecalculateContainerWeights();

            if (repaired)
            {
                BumpRevisionIfChanged(true);
            }
        }

        private void ImportContainers(InventoryContainerSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
            {
                RebuildContainersFromConfig();
                return;
            }

            for (var i = 0; i < snapshots.Length; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ContainerId))
                {
                    continue;
                }

                if (snapshot.ContainerType == InventoryContainerType.Temporary)
                {
                    Debug.LogWarning($"[NiumaInventory] 导入存档时跳过临时容器：{snapshot.ContainerId}。");
                    continue;
                }

                if (!_containerConfigs.TryGetValue(snapshot.ContainerId, out var config) || config == null)
                {
                    Debug.LogWarning($"[NiumaInventory] 导入存档时发现未知容器：{snapshot.ContainerId}，该容器及其中物品会被跳过。");
                    continue;
                }

                if (config.ContainerType == InventoryContainerType.Temporary)
                {
                    Debug.LogWarning($"[NiumaInventory] 导入存档时跳过配置为临时类型的容器：{snapshot.ContainerId}。");
                    continue;
                }

                var runtime = new InventoryContainerRuntime
                {
                    ContainerId = snapshot.ContainerId,
                    ContainerType = config.ContainerType,
                    SlotCount = Math.Max(0, config.SlotCount),
                    MaxWeight = Math.Max(0f, config.MaxWeight),
                    CurrentWeight = 0f,
                    IsUnlocked = snapshot.IsUnlocked
                };

                if (_containers.ContainsKey(runtime.ContainerId))
                {
                    Debug.LogWarning($"[NiumaInventory] 导入存档时发现重复容器：{runtime.ContainerId}，后续重复项已跳过。");
                    continue;
                }

                _containers[runtime.ContainerId] = runtime;
                _containerOrder.Add(runtime.ContainerId);
            }
        }

        private void ImportItems(InventoryItemSnapshot[] snapshots, out bool repaired)
        {
            repaired = false;
            if (snapshots == null)
            {
                return;
            }

            var occupiedSlots = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < snapshots.Length; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot == null
                    || string.IsNullOrWhiteSpace(snapshot.ItemId)
                    || string.IsNullOrWhiteSpace(snapshot.ContainerId)
                    || snapshot.Count <= 0)
                {
                    repaired = true;
                    continue;
                }

                if (!_containers.TryGetValue(snapshot.ContainerId, out var container) || container == null)
                {
                    Debug.LogWarning($"[NiumaInventory] 导入存档时跳过未知容器中的物品：ContainerId={snapshot.ContainerId}, ItemId={snapshot.ItemId}。");
                    repaired = true;
                    continue;
                }

                if (IsTemporaryContainer(snapshot.ContainerId))
                {
                    Debug.LogWarning($"[NiumaInventory] 导入存档时跳过临时容器中的物品：ContainerId={snapshot.ContainerId}, ItemId={snapshot.ItemId}。");
                    repaired = true;
                    continue;
                }

                var instanceId = snapshot.InstanceId;
                if (string.IsNullOrWhiteSpace(instanceId) || _items.ContainsKey(instanceId))
                {
                    instanceId = GenerateUniqueInstanceId();
                    repaired = true;
                }

                var slotIndex = snapshot.SlotIndex;
                if (!IsValidSlot(container, slotIndex) || occupiedSlots.Contains(CreateSlotKey(container.ContainerId, slotIndex)))
                {
                    slotIndex = FindFirstFreeSlot(container.ContainerId, occupiedSlots);
                    repaired = true;
                }

                if (!IsValidSlot(container, slotIndex))
                {
                    repaired = true;
                    continue;
                }

                var isMissing = !_definitionRegistry.Contains(snapshot.ItemId);
                if (isMissing)
                {
                    Debug.LogWarning($"[NiumaInventory] 导入存档时发现缺失物品定义：ItemId={snapshot.ItemId}，已按 MissingItem 保护导入。");
                }

                var acquiredOrder = snapshot.AcquiredOrder > 0 ? snapshot.AcquiredOrder : GetNextAcquiredOrder();
                if (acquiredOrder >= _nextAcquiredOrder)
                {
                    _nextAcquiredOrder = acquiredOrder + 1;
                }

                occupiedSlots.Add(CreateSlotKey(container.ContainerId, slotIndex));
                _items[instanceId] = new InventoryItemRuntime
                {
                    InstanceId = instanceId,
                    ItemId = snapshot.ItemId,
                    Count = snapshot.Count,
                    ContainerId = container.ContainerId,
                    SlotIndex = slotIndex,
                    IsLocked = snapshot.IsLocked,
                    IsMissing = isMissing,
                    AcquiredOrder = acquiredOrder,
                    CustomData = InventoryCustomDataEntry.CloneArray(snapshot.CustomData)
                };
            }
        }

        private void RebuildContainersFromConfig()
        {
            foreach (var pair in _containerConfigs)
            {
                _containers[pair.Key] = CreateContainerFromConfig(pair.Value);
                _containerOrder.Add(pair.Key);
            }
        }

        private void RebuildMissingConfiguredContainers()
        {
            foreach (var pair in _containerConfigs)
            {
                if (_containers.ContainsKey(pair.Key))
                {
                    continue;
                }

                _containers[pair.Key] = CreateContainerFromConfig(pair.Value);
                _containerOrder.Add(pair.Key);
            }
        }

        private bool IsTemporaryContainer(string containerId)
        {
            if (string.IsNullOrWhiteSpace(containerId))
            {
                return false;
            }

            if (_containers.TryGetValue(containerId, out var runtime) && runtime != null)
            {
                return runtime.ContainerType == InventoryContainerType.Temporary;
            }

            return _containerConfigs.TryGetValue(containerId, out var config)
                   && config != null
                   && config.ContainerType == InventoryContainerType.Temporary;
        }

        private AddPlan BuildAddPlan(AddItemRequest request, AddBatchPlanContext batchContext = null)
        {
            var plan = new AddPlan();
            if (request == null || string.IsNullOrWhiteSpace(request.ItemId) || request.Count <= 0)
            {
                return plan.Fail(InventoryFailureReason.InvalidRequest, "添加物品请求无效。");
            }

            if (!_definitionRegistry.TryGetDefinition(request.ItemId, out var definition))
            {
                return plan.Fail(InventoryFailureReason.ItemDefinitionMissing, "找不到物品定义。");
            }

            if (definition.ItemType == ItemType.None)
            {
                return plan.Fail(InventoryFailureReason.UnsupportedItemType, "物品类型不能为 None。");
            }

            if (definition.IsUnique && GetItemCount(request.ItemId) + (batchContext?.GetPlannedItemCount(request.ItemId) ?? 0) > 0)
            {
                return plan.Fail(InventoryFailureReason.UniqueItemAlreadyOwned, "唯一物品已经持有。");
            }

            if (definition.IsUnique && request.Count > 1 && !request.AllowPartial)
            {
                return plan.Fail(InventoryFailureReason.InvalidRequest, "唯一物品一次只能加入一个。");
            }

            var remaining = definition.IsUnique ? Math.Min(1, request.Count) : request.Count;
            var containers = GetCandidateContainers(request.TargetContainerId);
            if (containers.Count == 0)
            {
                return plan.Fail(InventoryFailureReason.ContainerMissing, "没有可用容器。");
            }

            if (request.TargetSlotIndex >= 0)
            {
                TryPlanTargetSlot(request, definition, containers, plan, batchContext, ref remaining);
            }
            else
            {
                TryPlanAutoStack(request, definition, containers, plan, batchContext, ref remaining);
                TryPlanPlannedStacks(request, definition, containers, plan, batchContext, ref remaining);
                TryPlanEmptySlots(request, definition, containers, plan, batchContext, ref remaining);
            }

            var overflow = request.Count - plan.TotalPlaced;
            plan.OverflowCount = Math.Max(0, overflow);
            if (remaining <= 0 || (request.AllowPartial && plan.TotalPlaced > 0))
            {
                plan.CanCommit = true;
                return plan;
            }

            return plan.Fail(
                plan.LastFailureReason != InventoryFailureReason.None ? plan.LastFailureReason : InventoryFailureReason.InventoryFull,
                string.IsNullOrWhiteSpace(plan.LastFailureMessage) ? "背包空间不足。" : plan.LastFailureMessage);
        }

        private void TryPlanTargetSlot(
            AddItemRequest request,
            ItemDefinition definition,
            List<InventoryContainerRuntime> containers,
            AddPlan plan,
            AddBatchPlanContext batchContext,
            ref int remaining)
        {
            var container = containers[0];
            if (!CanContainerAccept(container, definition, out var reason, out var message))
            {
                plan.RememberFailure(reason, message);
                return;
            }

            if (!IsValidSlot(container, request.TargetSlotIndex))
            {
                plan.RememberFailure(InventoryFailureReason.SlotInvalid, "目标格子索引无效。");
                return;
            }

            var target = FindItemAt(container.ContainerId, request.TargetSlotIndex);
            PlannedStack plannedStack = null;
            if (batchContext != null)
            {
                batchContext.TryGetPlannedStackAt(container.ContainerId, request.TargetSlotIndex, out plannedStack);
            }
            if (target != null && target.IsLocked)
            {
                plan.RememberFailure(InventoryFailureReason.ItemLocked, "目标物品堆已锁定，不能自动增加数量。");
                return;
            }

            var targetItemId = target != null ? target.ItemId : plannedStack?.ItemId;
            if (!string.IsNullOrWhiteSpace(targetItemId)
                && !string.Equals(targetItemId, request.ItemId, StringComparison.Ordinal))
            {
                plan.RememberFailure(InventoryFailureReason.SlotOccupied, "目标格子已被其他物品占用。");
                return;
            }

            var maxStack = GetEffectiveMaxStack(definition);
            var targetCount = target != null
                ? target.Count + (batchContext?.GetPlannedCountForInstance(target.InstanceId) ?? 0)
                : plannedStack?.Count ?? 0;
            var stackSpace = string.IsNullOrWhiteSpace(targetItemId) ? maxStack : Math.Max(0, maxStack - targetCount);
            var weightAllowed = GetAdditionalWeightCapacity(container, definition, GetTotalPlannedWeight(plan, batchContext, container.ContainerId));
            var count = Math.Min(remaining, Math.Min(stackSpace, weightAllowed));
            if (count <= 0)
            {
                plan.RememberFailure(weightAllowed <= 0 ? InventoryFailureReason.WeightLimitExceeded : InventoryFailureReason.InventoryFull, "目标格子容量不足。");
                return;
            }

            var targetInstanceId = target != null ? target.InstanceId : plannedStack?.VirtualInstanceId;
            plan.AddPlacement(container.ContainerId, request.TargetSlotIndex, count, targetInstanceId, GetItemWeight(definition) * count);
            remaining -= count;
        }

        private void TryPlanAutoStack(
            AddItemRequest request,
            ItemDefinition definition,
            List<InventoryContainerRuntime> containers,
            AddPlan plan,
            AddBatchPlanContext batchContext,
            ref int remaining)
        {
            var maxStack = GetEffectiveMaxStack(definition);
            if (maxStack <= 1)
            {
                return;
            }

            for (var c = 0; c < containers.Count && remaining > 0; c++)
            {
                var container = containers[c];
                if (!containerAllowsAutoStack(container) || !CanContainerAccept(container, definition, out _, out _))
                {
                    continue;
                }

                foreach (var pair in _items)
                {
                    var item = pair.Value;
                    if (item == null
                        || !string.Equals(item.ContainerId, container.ContainerId, StringComparison.Ordinal)
                        || !string.Equals(item.ItemId, request.ItemId, StringComparison.Ordinal)
                        || item.IsLocked
                        || item.Count + (batchContext?.GetPlannedCountForInstance(item.InstanceId) ?? 0) >= maxStack)
                    {
                        continue;
                    }

                    var stackSpace = maxStack - item.Count - (batchContext?.GetPlannedCountForInstance(item.InstanceId) ?? 0);
                    var weightAllowed = GetAdditionalWeightCapacity(container, definition, GetTotalPlannedWeight(plan, batchContext, container.ContainerId));
                    var count = Math.Min(remaining, Math.Min(stackSpace, weightAllowed));
                    if (count <= 0)
                    {
                        continue;
                    }

                    plan.AddPlacement(container.ContainerId, item.SlotIndex, count, item.InstanceId, GetItemWeight(definition) * count);
                    remaining -= count;
                    if (remaining <= 0)
                    {
                        return;
                    }
                }
            }
        }

        private void TryPlanPlannedStacks(
            AddItemRequest request,
            ItemDefinition definition,
            List<InventoryContainerRuntime> containers,
            AddPlan plan,
            AddBatchPlanContext batchContext,
            ref int remaining)
        {
            if (batchContext == null || remaining <= 0)
            {
                return;
            }

            var maxStack = GetEffectiveMaxStack(definition);
            if (maxStack <= 1)
            {
                return;
            }

            for (var c = 0; c < containers.Count && remaining > 0; c++)
            {
                var container = containers[c];
                if (!containerAllowsAutoStack(container) || !CanContainerAccept(container, definition, out _, out _))
                {
                    continue;
                }

                var plannedStacks = batchContext.PlannedStacks;
                for (var i = 0; i < plannedStacks.Count && remaining > 0; i++)
                {
                    var stack = plannedStacks[i];
                    if (stack == null
                        || stack.IsExistingStack
                        || !string.Equals(stack.ContainerId, container.ContainerId, StringComparison.Ordinal)
                        || !string.Equals(stack.ItemId, request.ItemId, StringComparison.Ordinal)
                        || stack.Count >= maxStack)
                    {
                        continue;
                    }

                    var stackSpace = maxStack - stack.Count;
                    var weightAllowed = GetAdditionalWeightCapacity(container, definition, GetTotalPlannedWeight(plan, batchContext, container.ContainerId));
                    var count = Math.Min(remaining, Math.Min(stackSpace, weightAllowed));
                    if (count <= 0)
                    {
                        continue;
                    }

                    plan.AddPlacement(container.ContainerId, stack.SlotIndex, count, stack.VirtualInstanceId, GetItemWeight(definition) * count);
                    remaining -= count;
                }
            }
        }

        private void TryPlanEmptySlots(
            AddItemRequest request,
            ItemDefinition definition,
            List<InventoryContainerRuntime> containers,
            AddPlan plan,
            AddBatchPlanContext batchContext,
            ref int remaining)
        {
            var maxStack = GetEffectiveMaxStack(definition);
            for (var c = 0; c < containers.Count && remaining > 0; c++)
            {
                var container = containers[c];
                if (!CanContainerAccept(container, definition, out var reason, out var message))
                {
                    plan.RememberFailure(reason, message);
                    continue;
                }

                for (var slot = 0; slot < container.SlotCount && remaining > 0; slot++)
                {
                    if (FindItemAt(container.ContainerId, slot) != null
                        || plan.IsSlotPlanned(container.ContainerId, slot)
                        || (batchContext?.IsSlotPlanned(container.ContainerId, slot) ?? false))
                    {
                        continue;
                    }

                    var weightAllowed = GetAdditionalWeightCapacity(container, definition, GetTotalPlannedWeight(plan, batchContext, container.ContainerId));
                    var count = Math.Min(remaining, Math.Min(maxStack, weightAllowed));
                    if (count <= 0)
                    {
                        plan.RememberFailure(InventoryFailureReason.WeightLimitExceeded, "容器重量不足。");
                        break;
                    }

                    plan.AddPlacement(container.ContainerId, slot, count, null, GetItemWeight(definition) * count);
                    remaining -= count;
                }
            }
        }

        private InventoryOperationResult ValidateRemoveRequest(RemoveItemRequest request, out List<RemoveStep> removeSteps)
        {
            removeSteps = new List<RemoveStep>();
            if (request == null || request.Count <= 0)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "移除请求无效。");
            }

            if (!string.IsNullOrWhiteSpace(request.InstanceId))
            {
                if (!_items.TryGetValue(request.InstanceId, out var item) || item == null)
                {
                    return InventoryOperationResult.Failed(InventoryFailureReason.ItemNotFound, "找不到要移除的物品实例。");
                }

                if (item.IsLocked)
                {
                    return InventoryOperationResult.Failed(InventoryFailureReason.ItemLocked, "锁定物品不能移除。");
                }

                if (item.IsMissing)
                {
                    return InventoryOperationResult.Failed(InventoryFailureReason.ItemDefinitionMissing, "缺失物品定义的物品不能移除。");
                }

                if (request.Count > item.Count)
                {
                    return InventoryOperationResult.Failed(InventoryFailureReason.NotEnoughCount, "物品数量不足。");
                }

                removeSteps.Add(new RemoveStep(item.InstanceId, request.Count));
                return InventoryOperationResult.Success(message: "可以移除物品。");
            }

            if (string.IsNullOrWhiteSpace(request.ItemId))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "ItemId 和 InstanceId 不能同时为空。");
            }

            var need = request.Count;
            var totalIncludingLocked = 0;
            var blockedByLocked = false;
            var blockedByMissing = false;
            foreach (var pair in _items)
            {
                var item = pair.Value;
                if (item == null || !string.Equals(item.ItemId, request.ItemId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.ContainerId)
                    && !string.Equals(item.ContainerId, request.ContainerId, StringComparison.Ordinal))
                {
                    continue;
                }

                totalIncludingLocked += item.Count;
                if (item.IsLocked)
                {
                    blockedByLocked = true;
                    continue;
                }

                if (item.IsMissing)
                {
                    blockedByMissing = true;
                    continue;
                }

                if (need <= 0)
                {
                    continue;
                }

                var removeCount = Math.Min(need, item.Count);
                removeSteps.Add(new RemoveStep(item.InstanceId, removeCount));
                need -= removeCount;
            }

            if (need <= 0)
            {
                return InventoryOperationResult.Success(message: "可以移除物品。");
            }

            if (totalIncludingLocked >= request.Count && blockedByMissing)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemDefinitionMissing, "可用数量不足，部分物品缺失定义。");
            }

            if (totalIncludingLocked >= request.Count && blockedByLocked)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemLocked, "可用数量不足，部分物品被锁定。");
            }

            return InventoryOperationResult.Failed(InventoryFailureReason.NotEnoughCount, "物品数量不足。");
        }

        private InventoryOperationResult MoveToEmptySlot(InventoryItemRuntime source, ItemDefinition definition, InventoryContainerRuntime targetContainer, int targetSlotIndex)
        {
            if (!CanAddWeight(targetContainer, definition, source.Count, source.ContainerId == targetContainer.ContainerId))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.WeightLimitExceeded, "目标容器重量不足。");
            }

            source.ContainerId = targetContainer.ContainerId;
            source.SlotIndex = targetSlotIndex;
            RecalculateContainerWeights();
            BumpRevisionIfChanged(true);
            return InventoryOperationResult.Success(null, null, new[] { source.ToSnapshot() });
        }

        private InventoryOperationResult MergeRuntimeStacks(InventoryItemRuntime source, InventoryItemRuntime target)
        {
            if (source == null || target == null || source == target)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "合并请求无效。");
            }

            if (source.IsLocked || target.IsLocked)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemLocked, "锁定物品不能合并。");
            }

            if (!string.Equals(source.ItemId, target.ItemId, StringComparison.Ordinal))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.UnsupportedItemType, "不同物品不能合并。");
            }

            if (!_definitionRegistry.TryGetDefinition(source.ItemId, out var definition))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemDefinitionMissing, "找不到物品定义，不能合并。");
            }

            var maxStack = GetEffectiveMaxStack(definition);
            if (maxStack <= 1)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "不可堆叠物品不能合并。");
            }

            var space = maxStack - target.Count;
            if (space <= 0)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InventoryFull, "目标堆叠已满。");
            }

            if (!_containers.TryGetValue(source.ContainerId, out var sourceContainer) || sourceContainer == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ContainerMissing, "找不到源容器。");
            }

            if (!_containers.TryGetValue(target.ContainerId, out var targetContainer) || targetContainer == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ContainerMissing, "找不到目标容器。");
            }

            if (!string.Equals(source.ContainerId, target.ContainerId, StringComparison.Ordinal)
                && (!IsContainerManuallyMovable(sourceContainer) || !IsContainerManuallyMovable(targetContainer)))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemCannotMove, "源容器或目标容器不允许手动移动。");
            }

            if (!CanContainerAccept(targetContainer, definition, out var reason, out var message))
            {
                return InventoryOperationResult.Failed(reason, message);
            }

            var sameContainer = string.Equals(source.ContainerId, target.ContainerId, StringComparison.Ordinal);
            var weightAllowed = sameContainer ? source.Count : GetAdditionalWeightCapacity(targetContainer, definition, 0f);
            var moved = Math.Min(source.Count, Math.Min(space, weightAllowed));
            if (moved <= 0)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.WeightLimitExceeded, "目标容器重量不足。");
            }

            source.Count -= moved;
            target.Count += moved;

            var removedItems = new List<InventoryItemSnapshot>();
            var changedItems = new List<InventoryItemSnapshot> { target.ToSnapshot() };
            if (source.Count <= 0)
            {
                removedItems.Add(source.ToSnapshot());
                _items.Remove(source.InstanceId);
            }
            else
            {
                changedItems.Add(source.ToSnapshot());
            }

            RecalculateContainerWeights();
            BumpRevisionIfChanged(true);
            return InventoryOperationResult.Success(null, removedItems.ToArray(), changedItems.ToArray());
        }

        private InventoryOperationResult SwapItems(
            InventoryItemRuntime source,
            InventoryItemRuntime target,
            InventoryContainerRuntime sourceContainer,
            InventoryContainerRuntime targetContainer,
            ItemDefinition sourceDefinition)
        {
            if (target.IsLocked)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemLocked, "目标物品被锁定，不能交换。");
            }

            if (!TryGetMovableDefinition(target, out var targetDefinition, out var failed))
            {
                return failed;
            }

            if (!CanContainerAccept(sourceContainer, targetDefinition, out var reason, out var message))
            {
                return InventoryOperationResult.Failed(reason, message);
            }

            if (!CanContainerAccept(targetContainer, sourceDefinition, out reason, out message))
            {
                return InventoryOperationResult.Failed(reason, message);
            }

            if (!CanSwapWeight(source, target, sourceContainer, targetContainer, sourceDefinition, targetDefinition))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.WeightLimitExceeded, "交换后容器重量会超限。");
            }

            var sourceContainerId = source.ContainerId;
            var sourceSlot = source.SlotIndex;
            source.ContainerId = target.ContainerId;
            source.SlotIndex = target.SlotIndex;
            target.ContainerId = sourceContainerId;
            target.SlotIndex = sourceSlot;

            RecalculateContainerWeights();
            BumpRevisionIfChanged(true);
            return InventoryOperationResult.Success(null, null, new[] { source.ToSnapshot(), target.ToSnapshot() });
        }

        private InventoryOperationResult SetLockState(string instanceId, bool locked)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.InvalidRequest, "实例 ID 为空。");
            }

            if (!_items.TryGetValue(instanceId, out var item) || item == null)
            {
                return InventoryOperationResult.Failed(InventoryFailureReason.ItemNotFound, "找不到物品实例。");
            }

            if (item.IsLocked == locked)
            {
                return InventoryOperationResult.Success(message: "锁定状态未变化。");
            }

            item.IsLocked = locked;
            BumpRevisionIfChanged(true);
            return InventoryOperationResult.Success(changedItems: new[] { item.ToSnapshot() });
        }

        private bool TryGetMovableDefinition(InventoryItemRuntime item, out ItemDefinition definition, out InventoryOperationResult failed)
        {
            definition = null;
            failed = null;
            if (item == null)
            {
                failed = InventoryOperationResult.Failed(InventoryFailureReason.ItemNotFound, "找不到物品实例。");
                return false;
            }

            if (item.IsMissing)
            {
                failed = InventoryOperationResult.Failed(InventoryFailureReason.ItemDefinitionMissing, "缺失物品定义的物品不能移动。");
                return false;
            }

            if (item.IsLocked)
            {
                failed = InventoryOperationResult.Failed(InventoryFailureReason.ItemLocked, "锁定物品不能移动。");
                return false;
            }

            if (!_definitionRegistry.TryGetDefinition(item.ItemId, out definition))
            {
                failed = InventoryOperationResult.Failed(InventoryFailureReason.ItemDefinitionMissing, "找不到物品定义，不能移动。");
                return false;
            }

            if (definition.ItemType == ItemType.None || !definition.CanMove)
            {
                failed = InventoryOperationResult.Failed(InventoryFailureReason.ItemCannotMove, "物品不允许移动。");
                return false;
            }

            return true;
        }

        private bool TryGetContainerPairForMove(
            string sourceContainerId,
            string targetContainerId,
            ItemDefinition movingDefinition,
            out InventoryContainerRuntime sourceContainer,
            out InventoryContainerRuntime targetContainer,
            out InventoryOperationResult failed)
        {
            sourceContainer = null;
            targetContainer = null;
            failed = null;

            if (string.IsNullOrWhiteSpace(targetContainerId)
                || !_containers.TryGetValue(targetContainerId, out targetContainer)
                || targetContainer == null)
            {
                failed = InventoryOperationResult.Failed(InventoryFailureReason.ContainerMissing, "找不到目标容器。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourceContainerId)
                || !_containers.TryGetValue(sourceContainerId, out sourceContainer)
                || sourceContainer == null)
            {
                failed = InventoryOperationResult.Failed(InventoryFailureReason.ContainerMissing, "找不到源容器。");
                return false;
            }

            if (!IsContainerManuallyMovable(sourceContainer) || !IsContainerManuallyMovable(targetContainer))
            {
                failed = InventoryOperationResult.Failed(InventoryFailureReason.ItemCannotMove, "源容器或目标容器不允许手动移动。");
                return false;
            }

            if (!CanContainerAccept(targetContainer, movingDefinition, out var reason, out var message))
            {
                failed = InventoryOperationResult.Failed(reason, message);
                return false;
            }

            return true;
        }

        private List<InventoryContainerRuntime> GetCandidateContainers(string targetContainerId)
        {
            var result = new List<InventoryContainerRuntime>();
            if (!string.IsNullOrWhiteSpace(targetContainerId))
            {
                if (_containers.TryGetValue(targetContainerId, out var target) && target != null)
                {
                    result.Add(target);
                }

                return result;
            }

            for (var i = 0; i < _containerOrder.Count; i++)
            {
                if (_containers.TryGetValue(_containerOrder[i], out var container) && container != null)
                {
                    result.Add(container);
                }
            }

            return result;
        }

        private bool CanContainerAccept(
            InventoryContainerRuntime container,
            ItemDefinition definition,
            out InventoryFailureReason reason,
            out string message)
        {
            reason = InventoryFailureReason.None;
            message = null;

            if (container == null || string.IsNullOrWhiteSpace(container.ContainerId))
            {
                reason = InventoryFailureReason.ContainerMissing;
                message = "容器不存在。";
                return false;
            }

            if (!container.IsUnlocked || container.SlotCount <= 0)
            {
                reason = InventoryFailureReason.InventoryFull;
                message = "容器未解锁或没有格子。";
                return false;
            }

            if (definition == null || definition.ItemType == ItemType.None)
            {
                reason = InventoryFailureReason.UnsupportedItemType;
                message = "物品类型无效。";
                return false;
            }

            _containerConfigs.TryGetValue(container.ContainerId, out var config);
            if (config == null)
            {
                return true;
            }

            if (config.AcceptedItemTypes != null && config.AcceptedItemTypes.Length > 0)
            {
                var accepted = false;
                for (var i = 0; i < config.AcceptedItemTypes.Length; i++)
                {
                    if (config.AcceptedItemTypes[i] == definition.ItemType && config.AcceptedItemTypes[i] != ItemType.None)
                    {
                        accepted = true;
                        break;
                    }
                }

                if (!accepted)
                {
                    reason = InventoryFailureReason.UnsupportedItemType;
                    message = "容器不接受该物品类型。";
                    return false;
                }
            }

            if (HasAnyTag(definition.Tags, config.RejectTags))
            {
                reason = InventoryFailureReason.UnsupportedItemType;
                message = "物品命中容器拒绝标签。";
                return false;
            }

            if (config.AcceptedTags != null
                && config.AcceptedTags.Length > 0
                && !HasAnyTag(definition.Tags, config.AcceptedTags))
            {
                reason = InventoryFailureReason.UnsupportedItemType;
                message = "物品未命中容器允许标签。";
                return false;
            }

            return true;
        }

        private bool IsContainerManuallyMovable(InventoryContainerRuntime container)
        {
            if (container == null)
            {
                return false;
            }

            return !_containerConfigs.TryGetValue(container.ContainerId, out var config)
                   || config == null
                   || config.AllowManualMove;
        }

        private bool containerAllowsAutoStack(InventoryContainerRuntime container)
        {
            return container != null
                   && (!_containerConfigs.TryGetValue(container.ContainerId, out var config)
                       || config == null
                       || config.AllowAutoStack);
        }

        private static bool HasAnyTag(string[] itemTags, string[] ruleTags)
        {
            if (itemTags == null || ruleTags == null || itemTags.Length == 0 || ruleTags.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < itemTags.Length; i++)
            {
                for (var j = 0; j < ruleTags.Length; j++)
                {
                    if (string.Equals(itemTags[i], ruleTags[j], StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private InventoryContainerRuntime CreateContainerFromConfig(InventoryContainerConfig config)
        {
            return new InventoryContainerRuntime
            {
                ContainerId = config.ContainerId,
                ContainerType = config.ContainerType,
                SlotCount = Math.Max(0, config.SlotCount),
                MaxWeight = Math.Max(0f, config.MaxWeight),
                CurrentWeight = 0f,
                IsUnlocked = config.IsUnlockedByDefault
            };
        }

        private InventoryItemRuntime FindItemAt(string containerId, int slotIndex)
        {
            if (string.IsNullOrWhiteSpace(containerId) || slotIndex < 0)
            {
                return null;
            }

            var slotKey = CreateSlotKey(containerId, slotIndex);
            if (!_slotIndex.TryGetValue(slotKey, out var instanceId))
            {
                return null;
            }

            return _items.TryGetValue(instanceId, out var item) ? item : null;
        }

        private InventoryItemRuntime GetItemOrNull(string instanceId)
        {
            return !string.IsNullOrWhiteSpace(instanceId) && _items.TryGetValue(instanceId, out var item)
                ? item
                : null;
        }

        private int FindFirstEmptySlot(string containerId)
        {
            if (!_containers.TryGetValue(containerId, out var container) || container == null)
            {
                return -1;
            }

            for (var slot = 0; slot < container.SlotCount; slot++)
            {
                if (FindItemAt(containerId, slot) == null)
                {
                    return slot;
                }
            }

            return -1;
        }

        private int FindFirstFreeSlot(string containerId, HashSet<string> occupiedSlots)
        {
            if (!_containers.TryGetValue(containerId, out var container) || container == null)
            {
                return -1;
            }

            for (var slot = 0; slot < container.SlotCount; slot++)
            {
                var key = CreateSlotKey(containerId, slot);
                if (!occupiedSlots.Contains(key))
                {
                    return slot;
                }
            }

            return -1;
        }

        private static bool IsValidSlot(InventoryContainerRuntime container, int slotIndex)
        {
            return container != null && slotIndex >= 0 && slotIndex < container.SlotCount;
        }

        private int GetAdditionalWeightCapacity(InventoryContainerRuntime container, ItemDefinition definition, float plannedExtraWeight)
        {
            if (container == null)
            {
                return 0;
            }

            var itemWeight = GetItemWeight(definition);
            if (itemWeight <= 0f || container.MaxWeight <= 0f)
            {
                return int.MaxValue;
            }

            var available = container.MaxWeight - container.CurrentWeight - plannedExtraWeight;
            if (available <= 0f)
            {
                return 0;
            }

            return Math.Max(0, (int)Math.Floor((available + 0.0001f) / itemWeight));
        }

        private bool CanAddWeight(InventoryContainerRuntime container, ItemDefinition definition, int count, bool sameContainer)
        {
            if (sameContainer)
            {
                return true;
            }

            return GetAdditionalWeightCapacity(container, definition, 0f) >= count;
        }

        private bool CanSwapWeight(
            InventoryItemRuntime source,
            InventoryItemRuntime target,
            InventoryContainerRuntime sourceContainer,
            InventoryContainerRuntime targetContainer,
            ItemDefinition sourceDefinition,
            ItemDefinition targetDefinition)
        {
            if (sourceContainer == targetContainer)
            {
                return true;
            }

            var sourceCurrent = sourceContainer.CurrentWeight;
            var targetCurrent = targetContainer.CurrentWeight;
            var sourceWeight = GetItemWeight(sourceDefinition) * source.Count;
            var targetWeight = GetItemWeight(targetDefinition) * target.Count;
            var sourceAfter = sourceCurrent - sourceWeight + targetWeight;
            var targetAfter = targetCurrent - targetWeight + sourceWeight;

            return IsWeightAllowed(sourceContainer, sourceAfter) && IsWeightAllowed(targetContainer, targetAfter);
        }

        private static bool IsWeightAllowed(InventoryContainerRuntime container, float weight)
        {
            return container == null || container.MaxWeight <= 0f || weight <= container.MaxWeight + 0.0001f;
        }

        private static float GetItemWeight(ItemDefinition definition)
        {
            return definition != null ? Math.Max(0f, definition.Weight) : 0f;
        }

        private static int GetEffectiveMaxStack(ItemDefinition definition)
        {
            if (definition == null || definition.IsUnique)
            {
                return 1;
            }

            return Math.Max(1, definition.MaxStackCount);
        }

        private void RecalculateContainerWeights()
        {
            _slotIndex.Clear();
            foreach (var pair in _containers)
            {
                if (pair.Value != null)
                {
                    pair.Value.CurrentWeight = 0f;
                }
            }

            foreach (var pair in _items)
            {
                var item = pair.Value;
                if (item == null || !_containers.TryGetValue(item.ContainerId, out var container) || container == null)
                {
                    continue;
                }

                if (_definitionRegistry.TryGetDefinition(item.ItemId, out var definition))
                {
                    container.CurrentWeight += GetItemWeight(definition) * Math.Max(0, item.Count);
                }

                if (IsValidSlot(container, item.SlotIndex))
                {
                    var slotKey = CreateSlotKey(item.ContainerId, item.SlotIndex);
                    if (!_slotIndex.ContainsKey(slotKey))
                    {
                        _slotIndex[slotKey] = item.InstanceId;
                    }
                    else
                    {
                        Debug.LogWarning($"[NiumaInventory] 检测到格子索引冲突：ContainerId={item.ContainerId}, SlotIndex={item.SlotIndex}, InstanceId={item.InstanceId}。请检查导入数据或外部修改。");
                    }
                }
            }
        }

        private void RefreshMissingFlags()
        {
            foreach (var pair in _items)
            {
                var item = pair.Value;
                if (item != null)
                {
                    item.IsMissing = !_definitionRegistry.Contains(item.ItemId);
                }
            }
        }

        private void RebuildSlotIndex()
        {
            _slotIndex.Clear();
            foreach (var pair in _items)
            {
                var item = pair.Value;
                if (item == null
                    || !_containers.TryGetValue(item.ContainerId, out var container)
                    || !IsValidSlot(container, item.SlotIndex))
                {
                    continue;
                }

                var slotKey = CreateSlotKey(item.ContainerId, item.SlotIndex);
                if (!_slotIndex.ContainsKey(slotKey))
                {
                    _slotIndex[slotKey] = item.InstanceId;
                }
                else
                {
                    Debug.LogWarning($"[NiumaInventory] 重建格子索引时发现冲突：ContainerId={item.ContainerId}, SlotIndex={item.SlotIndex}, InstanceId={item.InstanceId}。");
                }
            }
        }

        private int CompareItems(InventoryItemRuntime left, InventoryItemRuntime right, InventorySortKey[] sortKeys)
        {
            for (var i = 0; i < sortKeys.Length; i++)
            {
                var result = CompareByKey(left, right, sortKeys[i]);
                if (result != 0)
                {
                    return result;
                }
            }

            return string.Compare(left?.InstanceId, right?.InstanceId, StringComparison.Ordinal);
        }

        private int CompareByKey(InventoryItemRuntime left, InventoryItemRuntime right, InventorySortKey key)
        {
            _definitionRegistry.TryGetDefinition(left?.ItemId, out var leftDefinition);
            _definitionRegistry.TryGetDefinition(right?.ItemId, out var rightDefinition);

            switch (key)
            {
                case InventorySortKey.ItemType:
                    return ((int)(leftDefinition != null ? leftDefinition.ItemType : ItemType.None))
                        .CompareTo((int)(rightDefinition != null ? rightDefinition.ItemType : ItemType.None));

                case InventorySortKey.Quality:
                    return -((int)(leftDefinition != null ? leftDefinition.Quality : ItemQuality.Common))
                        .CompareTo((int)(rightDefinition != null ? rightDefinition.Quality : ItemQuality.Common));

                case InventorySortKey.ItemId:
                    return string.Compare(left?.ItemId, right?.ItemId, StringComparison.Ordinal);

                case InventorySortKey.Count:
                    return -(left?.Count ?? 0).CompareTo(right?.Count ?? 0);

                case InventorySortKey.AcquiredOrder:
                    return (left?.AcquiredOrder ?? 0L).CompareTo(right?.AcquiredOrder ?? 0L);

                default:
                    return 0;
            }
        }

        private InventoryItemSnapshot[] CreateOverflowSnapshots(AddItemRequest request, int overflowCount)
        {
            if (request == null || overflowCount <= 0)
            {
                return Array.Empty<InventoryItemSnapshot>();
            }

            return new[]
            {
                new InventoryItemSnapshot
                {
                    ItemId = request.ItemId,
                    Count = overflowCount,
                    SlotIndex = -1,
                    CustomData = InventoryCustomDataEntry.CloneArray(request.CustomData)
                }
            };
        }

        private static AddItemRequest CloneAddRequestForBatch(AddItemRequest source, bool allowPartial, string batchSourceModule)
        {
            if (source == null)
            {
                return null;
            }

            return new AddItemRequest
            {
                ItemId = source.ItemId,
                Count = source.Count,
                TargetContainerId = source.TargetContainerId,
                TargetSlotIndex = source.TargetSlotIndex,
                AllowPartial = allowPartial,
                CustomData = InventoryCustomDataEntry.CloneArray(source.CustomData),
                SourceModule = string.IsNullOrWhiteSpace(source.SourceModule) ? batchSourceModule : source.SourceModule
            };
        }

        private static float GetTotalPlannedWeight(AddPlan plan, AddBatchPlanContext batchContext, string containerId)
        {
            var currentPlanWeight = plan != null ? plan.GetPlannedWeight(containerId) : 0f;
            var batchWeight = batchContext != null ? batchContext.GetPlannedWeight(containerId) : 0f;
            return currentPlanWeight + batchWeight;
        }

        private string GenerateUniqueInstanceId()
        {
            string id;
            do
            {
                id = InstanceIdPrefix + Guid.NewGuid().ToString("N");
            }
            while (_items.ContainsKey(id));

            return id;
        }

        private long GetNextAcquiredOrder()
        {
            return _nextAcquiredOrder++;
        }

        private static string CreateSlotKey(string containerId, int slotIndex)
        {
            return containerId + ":" + slotIndex;
        }

        private void BumpRevisionIfChanged(bool changed)
        {
            if (changed)
            {
                _revision++;
            }
        }

        private sealed class AddBatchPlanContext
        {
            private readonly Dictionary<string, float> _plannedWeightByContainer =
                new Dictionary<string, float>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _plannedCountByItemId =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _plannedCountByInstanceId =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, PlannedStack> _plannedStackBySlot =
                new Dictionary<string, PlannedStack>(StringComparer.Ordinal);
            private readonly HashSet<string> _plannedSlots =
                new HashSet<string>(StringComparer.Ordinal);
            private int _nextVirtualId = 1;

            public readonly List<PlannedStack> PlannedStacks = new List<PlannedStack>();

            public void Apply(AddItemRequest request, AddPlan plan)
            {
                if (request == null || plan == null || !plan.CanCommit)
                {
                    return;
                }

                AddPlannedItemCount(request.ItemId, plan.TotalPlaced);
                for (var i = 0; i < plan.Placements.Count; i++)
                {
                    var placement = plan.Placements[i];
                    AddWeight(placement.ContainerId, plan.GetPlacementWeight(i));

                    if (!string.IsNullOrWhiteSpace(placement.TargetInstanceId))
                    {
                        if (_plannedStackBySlot.TryGetValue(CreateSlotKey(placement.ContainerId, placement.SlotIndex), out var plannedStack)
                            && string.Equals(plannedStack.VirtualInstanceId, placement.TargetInstanceId, StringComparison.Ordinal))
                        {
                            plannedStack.Count += placement.Count;
                            continue;
                        }

                        AddPlannedInstanceCount(placement.TargetInstanceId, placement.Count);
                        continue;
                    }

                    var stack = new PlannedStack(
                        CreateVirtualInstanceId(),
                        request.ItemId,
                        placement.ContainerId,
                        placement.SlotIndex,
                        placement.Count,
                        false);
                    PlannedStacks.Add(stack);
                    var slotKey = CreateSlotKey(placement.ContainerId, placement.SlotIndex);
                    _plannedStackBySlot[slotKey] = stack;
                    _plannedSlots.Add(slotKey);
                }
            }

            public int GetPlannedItemCount(string itemId)
            {
                return !string.IsNullOrWhiteSpace(itemId) && _plannedCountByItemId.TryGetValue(itemId, out var count)
                    ? count
                    : 0;
            }

            public int GetPlannedCountForInstance(string instanceId)
            {
                return !string.IsNullOrWhiteSpace(instanceId) && _plannedCountByInstanceId.TryGetValue(instanceId, out var count)
                    ? count
                    : 0;
            }

            public float GetPlannedWeight(string containerId)
            {
                return !string.IsNullOrWhiteSpace(containerId) && _plannedWeightByContainer.TryGetValue(containerId, out var weight)
                    ? weight
                    : 0f;
            }

            public bool IsSlotPlanned(string containerId, int slotIndex)
            {
                return _plannedSlots.Contains(CreateSlotKey(containerId, slotIndex));
            }

            public bool TryGetPlannedStackAt(string containerId, int slotIndex, out PlannedStack stack)
            {
                return _plannedStackBySlot.TryGetValue(CreateSlotKey(containerId, slotIndex), out stack);
            }

            private void AddWeight(string containerId, float weight)
            {
                if (string.IsNullOrWhiteSpace(containerId) || weight <= 0f)
                {
                    return;
                }

                if (!_plannedWeightByContainer.ContainsKey(containerId))
                {
                    _plannedWeightByContainer[containerId] = 0f;
                }

                _plannedWeightByContainer[containerId] += weight;
            }

            private void AddPlannedItemCount(string itemId, int count)
            {
                if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
                {
                    return;
                }

                if (!_plannedCountByItemId.ContainsKey(itemId))
                {
                    _plannedCountByItemId[itemId] = 0;
                }

                _plannedCountByItemId[itemId] += count;
            }

            private void AddPlannedInstanceCount(string instanceId, int count)
            {
                if (string.IsNullOrWhiteSpace(instanceId) || count <= 0)
                {
                    return;
                }

                if (!_plannedCountByInstanceId.ContainsKey(instanceId))
                {
                    _plannedCountByInstanceId[instanceId] = 0;
                }

                _plannedCountByInstanceId[instanceId] += count;
            }

            private string CreateVirtualInstanceId()
            {
                return "__batch_preview_" + _nextVirtualId++;
            }
        }

        private sealed class PlannedStack
        {
            public readonly string VirtualInstanceId;
            public readonly string ItemId;
            public readonly string ContainerId;
            public readonly int SlotIndex;
            public readonly bool IsExistingStack;
            public int Count;

            public PlannedStack(
                string virtualInstanceId,
                string itemId,
                string containerId,
                int slotIndex,
                int count,
                bool isExistingStack)
            {
                VirtualInstanceId = virtualInstanceId;
                ItemId = itemId;
                ContainerId = containerId;
                SlotIndex = slotIndex;
                Count = count;
                IsExistingStack = isExistingStack;
            }
        }

        private sealed class AddPlan
        {
            private readonly Dictionary<string, float> _plannedWeightByContainer =
                new Dictionary<string, float>(StringComparer.Ordinal);
            private readonly HashSet<string> _plannedSlots =
                new HashSet<string>(StringComparer.Ordinal);
            private readonly List<float> _placementWeights = new List<float>();

            public readonly List<Placement> Placements = new List<Placement>();
            public bool CanCommit;
            public int TotalPlaced;
            public int OverflowCount;
            public InventoryFailureReason FailureReason = InventoryFailureReason.None;
            public string FailureMessage;
            public InventoryFailureReason LastFailureReason = InventoryFailureReason.None;
            public string LastFailureMessage;

            public AddPlan Fail(InventoryFailureReason reason, string message)
            {
                CanCommit = false;
                FailureReason = reason;
                FailureMessage = message;
                return this;
            }

            public void RememberFailure(InventoryFailureReason reason, string message)
            {
                LastFailureReason = reason;
                LastFailureMessage = message;
            }

            public void AddPlacement(string containerId, int slotIndex, int count, string targetInstanceId, float weight)
            {
                Placements.Add(new Placement(containerId, slotIndex, count, targetInstanceId));
                _placementWeights.Add(weight);
                TotalPlaced += count;

                if (!_plannedWeightByContainer.ContainsKey(containerId))
                {
                    _plannedWeightByContainer[containerId] = 0f;
                }

                _plannedWeightByContainer[containerId] += weight;
                if (string.IsNullOrWhiteSpace(targetInstanceId))
                {
                    _plannedSlots.Add(CreateSlotKey(containerId, slotIndex));
                }
            }

            public float GetPlannedWeight(string containerId)
            {
                return _plannedWeightByContainer.TryGetValue(containerId, out var weight) ? weight : 0f;
            }

            public bool IsSlotPlanned(string containerId, int slotIndex)
            {
                return _plannedSlots.Contains(CreateSlotKey(containerId, slotIndex));
            }

            public float GetPlacementWeight(int index)
            {
                return index >= 0 && index < _placementWeights.Count ? _placementWeights[index] : 0f;
            }
        }

        private readonly struct Placement
        {
            public readonly string ContainerId;
            public readonly int SlotIndex;
            public readonly int Count;
            public readonly string TargetInstanceId;

            public Placement(string containerId, int slotIndex, int count, string targetInstanceId)
            {
                ContainerId = containerId;
                SlotIndex = slotIndex;
                Count = count;
                TargetInstanceId = targetInstanceId;
            }
        }

        private readonly struct RemoveStep
        {
            public readonly string InstanceId;
            public readonly int Count;

            public RemoveStep(string instanceId, int count)
            {
                InstanceId = instanceId;
                Count = count;
            }
        }
    }
}
