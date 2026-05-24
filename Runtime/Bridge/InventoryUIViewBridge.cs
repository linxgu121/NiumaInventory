using System;
using System.Collections.Generic;
using NiumaInventory.Config;
using NiumaInventory.Controller;
using NiumaInventory.Data;
using NiumaInventory.Enum;
using UnityEngine;

namespace NiumaInventory.Bridge
{
    /// <summary>
    /// 背包模块到 UI 模块的数据驱动桥接层。
    /// 桥接层按背包修订号拉取容器和物品表现数据，不订阅事件，也不直接依赖具体 UI 框架。
    /// </summary>
    public sealed class InventoryUIViewBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("背包模块根控制器。请拖入场景中的 NiumaInventoryController；为空时可按配置自动查找。")]
        [SerializeField] private NiumaInventoryController inventoryController;

        [Tooltip("实现 IInventoryUIReceiver 的 UI 组件。桥接层会把整理后的背包表现数据交给它显示。")]
        [SerializeField] private MonoBehaviour inventoryUIReceiverProvider;

        [Header("自动查找")]
        [Tooltip("没有手动绑定背包控制器时，是否在场景中自动查找 NiumaInventoryController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindInventoryController = true;

        [Header("刷新策略")]
        [Tooltip("启用桥接层时是否立即刷新一次背包面板。")]
        [SerializeField] private bool refreshOnEnable = true;

        [Tooltip("是否在 LateUpdate 中按背包版本号自动刷新 UI。关闭后需要外部手动调用 RefreshInventoryPanel。")]
        [SerializeField] private bool refreshInLateUpdate = true;

        [Tooltip("没有背包服务或没有可展示容器时，是否发送 Cleared 更新给 UI 接收接口。")]
        [SerializeField] private bool notifyWhenCleared = true;

        [Header("运行时状态（只读）")]
        [Tooltip("当前选中容器 ID。为空时桥接层会自动选择第一个可展示容器。")]
        [SerializeField] private string selectedContainerId;

        [Tooltip("当前选中物品实例 ID。为空时表示没有选中物品。")]
        [SerializeField] private string selectedItemInstanceId;

        [Header("筛选")]
        [Tooltip("是否显示 Temporary 临时容器。正式背包 UI 通常关闭，调试面板可开启。")]
        [SerializeField] private bool includeTemporaryContainers;

        [Tooltip("是否显示未解锁容器。正式 UI 通常关闭，调试面板可开启。")]
        [SerializeField] private bool includeLockedContainers;

        [Header("日志")]
        [Tooltip("桥接层缺少必要引用或检测到 UI 刷新回流时是否打印警告。")]
        [SerializeField] private bool logWarnings = true;

        private readonly List<InventoryContainerViewData> _containerBuffer = new List<InventoryContainerViewData>();
        private readonly List<InventoryItemViewData> _itemBuffer = new List<InventoryItemViewData>();
        private readonly List<InventoryItemViewData> _containerItemBuffer = new List<InventoryItemViewData>();
        private readonly List<InventoryContainerSnapshot> _containerSnapshotBuffer = new List<InventoryContainerSnapshot>();
        private readonly List<InventoryItemSnapshot> _itemSnapshotBuffer = new List<InventoryItemSnapshot>();
        private IInventoryUIReceiver _receiver;
        private int _observedRevision = -1;
        private InventoryPanelViewData _lastPanelData;
        private bool _hadPanelData;
        private bool _isApplyingUpdate;
        private bool _refreshRequested;
        private int _lastBuildFailureRevision = int.MinValue;

        private void Reset()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            ResolveReferences(true);
            _observedRevision = -1;

            if (refreshOnEnable)
            {
                RefreshInventoryPanel();
            }
        }

        private void OnDisable()
        {
            _isApplyingUpdate = false;
            _refreshRequested = false;
        }

        private void LateUpdate()
        {
            if (_refreshRequested)
            {
                _refreshRequested = false;
                RefreshInventoryPanel();
                return;
            }

            if (!refreshInLateUpdate || !EnsureController())
            {
                return;
            }

            if (_observedRevision == inventoryController.InventoryRevision)
            {
                return;
            }

            RefreshInventoryPanel();
        }

        /// <summary>
        /// 手动刷新背包面板。
        /// 只读取背包状态，不执行背包命令、不修改运行时数据。
        /// </summary>
        public void RefreshInventoryPanel()
        {
            if (!EnsureController())
            {
                ApplyClearUpdate();
                return;
            }

            var targetRevision = inventoryController.InventoryRevision;
            InventoryPanelViewData panelData;
            try
            {
                panelData = BuildPanelViewData(targetRevision);
            }
            catch (Exception exception)
            {
                _observedRevision = -1;
                if (logWarnings && _lastBuildFailureRevision != targetRevision)
                {
                    Debug.LogError($"[NiumaInventoryUIBridge] 构建背包 UI 表现数据失败，桥接层会在下一次刷新时重试。Revision={targetRevision}, Error={exception.Message}", this);
                }

                _lastBuildFailureRevision = targetRevision;
                return;
            }

            _lastBuildFailureRevision = int.MinValue;
            _observedRevision = targetRevision;
            if (panelData == null || panelData.Containers.Length == 0)
            {
                ApplyClearUpdate();
                return;
            }

            _hadPanelData = true;
            ApplyRawUpdate(new InventoryUIUpdate(
                InventoryUIUpdateType.Refresh,
                _observedRevision,
                panelData,
                _lastPanelData));
            _lastPanelData = panelData;
        }

        /// <summary>
        /// 设置当前选中容器并刷新 UI。
        /// </summary>
        public void SetSelectedContainer(string containerId)
        {
            selectedContainerId = containerId;
            selectedItemInstanceId = null;
            RequestRefresh();
        }

        /// <summary>
        /// 设置当前选中物品并刷新 UI。
        /// </summary>
        public void SetSelectedItem(string instanceId)
        {
            selectedItemInstanceId = instanceId;
            RequestRefresh();
        }

        /// <summary>
        /// 清空当前选中物品并刷新 UI。
        /// </summary>
        public void ClearSelectedItem()
        {
            SetSelectedItem(null);
        }

        private InventoryPanelViewData BuildPanelViewData(int revision)
        {
            // UI 刷新只走轻量只读查询，不调用 ExportSnapshot，避免把存档导出路径当作界面数据源。
            inventoryController.CopyContainerSnapshots(_containerSnapshotBuffer);
            inventoryController.CopyItemSnapshots(_itemSnapshotBuffer);

            BuildAllItemViewData(_itemSnapshotBuffer);

            _containerBuffer.Clear();
            InventoryContainerViewData selectedContainer = null;
            for (var i = 0; i < _containerSnapshotBuffer.Count; i++)
            {
                var container = _containerSnapshotBuffer[i];
                if (!ShouldShowContainer(container))
                {
                    continue;
                }

                var containerData = BuildContainerViewData(container);
                _containerBuffer.Add(containerData);
                if (!string.IsNullOrWhiteSpace(selectedContainerId)
                    && string.Equals(containerData.ContainerId, selectedContainerId, StringComparison.Ordinal))
                {
                    selectedContainer = containerData;
                }
            }

            if (selectedContainer == null && _containerBuffer.Count > 0)
            {
                selectedContainer = _containerBuffer[0];
                selectedContainerId = selectedContainer.ContainerId;
            }

            var selectedItem = FindItemViewData(selectedItemInstanceId);
            if (!string.IsNullOrWhiteSpace(selectedItemInstanceId) && selectedItem == null)
            {
                selectedItemInstanceId = null;
            }

            if (selectedItem != null)
            {
                selectedContainerId = selectedItem.ContainerId;
                selectedContainer = FindContainerViewData(selectedItem.ContainerId, _containerBuffer);
            }

            return new InventoryPanelViewData
            {
                Revision = revision,
                Containers = _containerBuffer.ToArray(),
                AllItems = _itemBuffer.ToArray(),
                SelectedContainerId = selectedContainerId,
                SelectedContainer = selectedContainer,
                SelectedItemInstanceId = selectedItemInstanceId,
                SelectedItem = selectedItem
            };
        }

        private void BuildAllItemViewData(IReadOnlyList<InventoryItemSnapshot> items)
        {
            _itemBuffer.Clear();
            for (var i = 0; items != null && i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.InstanceId))
                {
                    continue;
                }

                _itemBuffer.Add(BuildItemViewData(item));
            }

            _itemBuffer.Sort(CompareItemViewData);
        }

        private InventoryContainerViewData BuildContainerViewData(InventoryContainerSnapshot container)
        {
            _containerItemBuffer.Clear();
            for (var i = 0; i < _itemBuffer.Count; i++)
            {
                var item = _itemBuffer[i];
                if (item != null && string.Equals(item.ContainerId, container.ContainerId, StringComparison.Ordinal))
                {
                    _containerItemBuffer.Add(item);
                }
            }

            _containerItemBuffer.Sort(CompareItemViewData);

            var displayName = container.ContainerId;
            if (inventoryController.TryGetContainerConfig(container.ContainerId, out var config)
                && config != null
                && !string.IsNullOrWhiteSpace(config.DisplayName))
            {
                displayName = config.DisplayName;
            }

            return new InventoryContainerViewData
            {
                ContainerId = container.ContainerId,
                DisplayName = displayName,
                ContainerType = container.ContainerType,
                SlotCount = container.SlotCount,
                MaxWeight = container.MaxWeight,
                CurrentWeight = container.CurrentWeight,
                IsUnlocked = container.IsUnlocked,
                Items = _containerItemBuffer.ToArray()
            };
        }

        private InventoryItemViewData BuildItemViewData(InventoryItemSnapshot item)
        {
            var hasDefinition = inventoryController.TryGetItemDefinition(item.ItemId, out var definition) && definition != null;
            var isMissing = item.IsMissing || !hasDefinition;
            return new InventoryItemViewData
            {
                InstanceId = item.InstanceId,
                ItemId = item.ItemId,
                DisplayName = ResolveDisplayName(item, definition, isMissing),
                Description = isMissing ? string.Empty : definition.Description,
                IconAddress = isMissing ? string.Empty : definition.IconAddress,
                ItemType = isMissing ? ItemType.None : definition.ItemType,
                Quality = isMissing ? ItemQuality.Common : definition.Quality,
                Tags = isMissing ? Array.Empty<string>() : CopyStringArray(definition.Tags),
                Count = item.Count,
                MaxStackCount = isMissing ? 1 : Math.Max(1, definition.MaxStackCount),
                Weight = isMissing ? 0f : Math.Max(0f, definition.Weight),
                TotalWeight = isMissing ? 0f : Math.Max(0f, definition.Weight) * Math.Max(0, item.Count),
                ContainerId = item.ContainerId,
                SlotIndex = item.SlotIndex,
                IsMissing = isMissing,
                AcquiredOrder = item.AcquiredOrder,
                IsLocked = item.IsLocked,
                CanUse = !isMissing && definition.CanUse,
                CanMove = !isMissing && definition.CanMove,
                CanDiscard = !isMissing && definition.CanDiscard,
                CanTrade = !isMissing && definition.CanTrade,
                CanSell = !isMissing && definition.CanSell,
                CanDecompose = !isMissing && definition.CanDecompose,
                CanMail = !isMissing && definition.CanMail
            };
        }

        private bool ShouldShowContainer(InventoryContainerSnapshot container)
        {
            if (container == null || string.IsNullOrWhiteSpace(container.ContainerId))
            {
                return false;
            }

            if (!includeTemporaryContainers && container.ContainerType == InventoryContainerType.Temporary)
            {
                return false;
            }

            if (!includeLockedContainers && !container.IsUnlocked)
            {
                return false;
            }

            return true;
        }

        private InventoryItemViewData FindItemViewData(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            for (var i = 0; i < _itemBuffer.Count; i++)
            {
                var item = _itemBuffer[i];
                if (item != null && string.Equals(item.InstanceId, instanceId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static InventoryContainerViewData FindContainerViewData(
            string containerId,
            List<InventoryContainerViewData> containers)
        {
            if (string.IsNullOrWhiteSpace(containerId) || containers == null)
            {
                return null;
            }

            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                if (container != null && string.Equals(container.ContainerId, containerId, StringComparison.Ordinal))
                {
                    return container;
                }
            }

            return null;
        }

        private void ApplyClearUpdate()
        {
            if (!notifyWhenCleared && !_hadPanelData)
            {
                return;
            }

            _receiver = ResolveReceiver(true);
            ApplyRawUpdate(new InventoryUIUpdate(
                InventoryUIUpdateType.Cleared,
                _observedRevision,
                null,
                _lastPanelData));

            _hadPanelData = false;
            _lastPanelData = null;
        }

        private void ApplyRawUpdate(InventoryUIUpdate update)
        {
            _receiver = ResolveReceiver(true);
            if (_receiver == null)
            {
                return;
            }

            if (_isApplyingUpdate)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaInventoryUIBridge] 检测到 UI 刷新重入，已跳过本次 ApplyInventoryUpdate。请不要在 IInventoryUIReceiver.ApplyInventoryUpdate 中修改背包状态。", this);
                }

                return;
            }

            var revisionBeforeApply = inventoryController != null ? inventoryController.InventoryRevision : _observedRevision;
            _isApplyingUpdate = true;
            try
            {
                _receiver.ApplyInventoryUpdate(update);
            }
            finally
            {
                _isApplyingUpdate = false;
            }

            if (inventoryController != null && inventoryController.InventoryRevision != revisionBeforeApply)
            {
                _observedRevision = -1;
                _refreshRequested = true;
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaInventoryUIBridge] IInventoryUIReceiver.ApplyInventoryUpdate 内修改了背包数据，桥接层已请求下一帧重新刷新。请把背包命令放到输入、交互或业务管线中处理。", this);
                }
            }
        }

        private void RequestRefresh()
        {
            _observedRevision = -1;
            _refreshRequested = true;
        }

        private bool EnsureController()
        {
            ResolveInventoryController(true);
            return inventoryController != null;
        }

        private void ResolveReferences(bool logMissing)
        {
            ResolveInventoryController(logMissing);
            _receiver = ResolveReceiver(logMissing);
        }

        private void ResolveInventoryController(bool logMissing)
        {
            if (inventoryController != null)
            {
                return;
            }

            if (autoFindInventoryController)
            {
#if UNITY_2023_1_OR_NEWER
                inventoryController = FindFirstObjectByType<NiumaInventoryController>();
#else
                inventoryController = FindObjectOfType<NiumaInventoryController>();
#endif
            }

            if (inventoryController == null && logWarnings && logMissing)
            {
                Debug.LogWarning("[NiumaInventoryUIBridge] 未找到 NiumaInventoryController，请在 Inspector 中绑定背包控制器。", this);
            }
        }

        private IInventoryUIReceiver ResolveReceiver(bool logMissing)
        {
            var receiver = inventoryUIReceiverProvider as IInventoryUIReceiver;
            if (receiver == null && logWarnings && logMissing && inventoryUIReceiverProvider != null)
            {
                Debug.LogWarning("[NiumaInventoryUIBridge] Inventory UI Receiver Provider 没有实现 IInventoryUIReceiver。", this);
            }

            return receiver;
        }

        private static string ResolveDisplayName(InventoryItemSnapshot item, ItemDefinition definition, bool isMissing)
        {
            if (isMissing)
            {
                return string.IsNullOrWhiteSpace(item?.ItemId) ? "未知物品" : $"未知物品({item.ItemId})";
            }

            return !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : definition.ItemId;
        }

        private static int CompareItemViewData(InventoryItemViewData left, InventoryItemViewData right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var containerCompare = string.CompareOrdinal(left.ContainerId, right.ContainerId);
            if (containerCompare != 0)
            {
                return containerCompare;
            }

            var slotCompare = left.SlotIndex.CompareTo(right.SlotIndex);
            if (slotCompare != 0)
            {
                return slotCompare;
            }

            return left.AcquiredOrder.CompareTo(right.AcquiredOrder);
        }

        private static string[] CopyStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }
}
