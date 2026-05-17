using System;
using NiumaInteract.Core.Data;
using NiumaInteract.Core.Enum;
using NiumaInteract.Core.Interface;
using NiumaInventory.Controller;
using NiumaInventory.Data;
using NiumaInventory.Request;
using UnityEngine;

namespace NiumaInventory.Bridge.Interact
{
    /// <summary>
    /// 背包拾取交互物。
    /// 负责把 NiumaInteract 的交互触发转换为 NiumaInventory 的 AddItem 请求。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryPickupInteractable : MonoBehaviour, IInteractable, IInteractionPromptPolicy
    {
        [Header("背包引用")]
        [Tooltip("背包模块根控制器。正式场景建议手动绑定 NiumaInventoryController。")]
        [SerializeField] private NiumaInventoryController inventoryController;

        [Tooltip("背包控制器为空时是否自动在场景中查找。调试阶段可开启；正式多场景建议手动绑定。")]
        [SerializeField] private bool autoFindInventoryController = true;

        [Header("拾取物品")]
        [Tooltip("拾取后加入背包的物品 ID，必须对应 ItemDefinition.ItemId。")]
        [SerializeField] private string itemId;

        [Tooltip("场景中当前剩余可拾取数量。拾取全部成功后物体会隐藏或销毁。")]
        [SerializeField] private int count = 1;

        [Tooltip("目标容器 ID。为空时由背包服务按规则自动选择容器。")]
        [SerializeField] private string targetContainerId;

        [Tooltip("目标格子索引。小于 0 时由背包服务自动选择格子。")]
        [SerializeField] private int targetSlotIndex = -1;

        [Tooltip("是否允许部分拾取。开启后背包只能放下一部分时，会保留未放入的剩余数量在场景物体上。")]
        [SerializeField] private bool allowPartialPickup = true;

        [Tooltip("加入背包时写入新物品实例的扩展数据。可用于记录来源、场景物 ID 或临时调试标记。")]
        [SerializeField] private InventoryCustomDataEntry[] customData = Array.Empty<InventoryCustomDataEntry>();

        [Header("显示")]
        [Tooltip("交互 ID。正式内容建议填写稳定 ID，用于任务、存档和调试追踪；为空时回退为物体名称。")]
        [SerializeField] private string interactionId;

        [Tooltip("交互显示名称。为空时使用 ItemId。")]
        [SerializeField] private string displayName = "物品";

        [Tooltip("显示名称中是否追加剩余数量。例如“草药 x3”。部分拾取后提示会显示最新剩余数量。")]
        [SerializeField] private bool appendCountToDisplayName = true;

        [Tooltip("交互提示文本，例如“拾取”。")]
        [SerializeField] private string promptText = "拾取";

        [Tooltip("交互提示类型。拾取物通常使用 WorldSpace 或 ScreenSpace。")]
        [SerializeField] private PromptType promptType = PromptType.WorldSpace;

        [Tooltip("世界空间提示挂点。为空时使用 InteractionTransform。")]
        [SerializeField] private Transform promptAnchor;

        [Header("交互")]
        [Tooltip("交互检测使用的稳定位置源。为空时使用当前物体 Transform。")]
        [SerializeField] private Transform interactionTransform;

        [Tooltip("交互排序优先级。数值越大越容易成为当前焦点目标。")]
        [SerializeField] private float priority = 1f;

        [Tooltip("长按触发阈值，单位秒。短按拾取保持 0。")]
        [SerializeField] private float longPressDuration;

        [Tooltip("该拾取物支持的交互类型。普通拾取使用 Short；需要长按拾取时选择 Long。")]
        [SerializeField] private InteractKind supportedKinds = InteractKind.Short;

        [Tooltip("CanInteract 阶段是否预检查背包能否放入。开启后背包满时不会触发成功交互，避免任务桥接误认为拾取成功。")]
        [SerializeField] private bool precheckInventoryCapacity = true;

        [Header("拾取成功表现")]
        [Tooltip("全部拾取成功后是否销毁整个 GameObject。关闭时会按配置隐藏或禁用物体。")]
        [SerializeField] private bool destroyGameObjectOnPickup;

        [Tooltip("全部拾取成功后是否禁用整个 GameObject。一次性拾取物通常开启。")]
        [SerializeField] private bool deactivateGameObjectOnPickup = true;

        [Tooltip("全部拾取成功后需要隐藏的表现根节点。为空时不额外处理表现节点。")]
        [SerializeField] private GameObject visualRoot;

        [Tooltip("全部拾取成功后需要禁用的碰撞体。为空时默认禁用当前物体上的 Collider。")]
        [SerializeField] private Collider[] collidersToDisable;

        [Header("日志")]
        [Tooltip("拾取失败或缺少引用时是否输出中文警告。")]
        [SerializeField] private bool logWarnings = true;

        [Tooltip("拾取成功时是否输出调试日志。")]
        [SerializeField] private bool logSuccess;

        private bool _picked;

        public string InteractionId => string.IsNullOrWhiteSpace(interactionId) ? gameObject.name : interactionId;
        public Transform InteractionTransform => interactionTransform != null ? interactionTransform : transform;
        public string DisplayName => BuildDisplayName();
        public string PromptText => BuildPromptText();
        public PromptType PromptType => promptType;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : InteractionTransform;
        public float Priority => priority;
        public float LongPressDuration => longPressDuration;
        public InteractKind SupportedKinds => supportedKinds;
        public bool SuppressPromptAfterSuccess => _picked || count <= 0;

        /// <summary>
        /// 只有拾取物仍有效、配置完整且背包有接收能力时，才允许交互系统触发拾取。
        /// </summary>
        public bool CanInteract(in InteractionContext context)
        {
            if (!isActiveAndEnabled
                || _picked
                || string.IsNullOrWhiteSpace(itemId)
                || count <= 0
                || !HasValidSupportedKind())
            {
                return false;
            }

            if (!ResolveInventoryController(false))
            {
                return false;
            }

            if (!precheckInventoryCapacity)
            {
                return true;
            }

            var result = inventoryController.CanAddItem(CreateAddRequest());
            return result != null && result.Succeeded;
        }

        /// <summary>
        /// 执行拾取：向背包添加物品，成功后根据剩余数量决定隐藏物体或保留场景物。
        /// </summary>
        public void Interact(in InteractionRequest request)
        {
            if (_picked || (supportedKinds & request.Kind) != request.Kind)
            {
                return;
            }

            if (!ResolveInventoryController(true))
            {
                return;
            }

            var result = inventoryController.AddItem(CreateAddRequest());
            if (result == null || !result.Succeeded)
            {
                LogPickupFailed(result);
                return;
            }

            if (!HasAcceptedItems(result))
            {
                LogInconsistentSuccess(result);
                return;
            }

            var overflowCount = GetOverflowCount(result);
            if (overflowCount > 0)
            {
                count = overflowCount;
                if (logSuccess)
                {
                    Debug.Log($"[InventoryPickupInteractable] 部分拾取成功，场景物保留剩余数量：ItemId={itemId}, Remaining={count}。", this);
                }

                return;
            }

            count = 0;
            _picked = true;
            if (logSuccess)
            {
                Debug.Log($"[InventoryPickupInteractable] 拾取成功：ItemId={itemId}。", this);
            }

            HideAfterPickup();
        }

        private AddItemRequest CreateAddRequest()
        {
            return new AddItemRequest
            {
                ItemId = itemId,
                Count = count,
                TargetContainerId = targetContainerId,
                TargetSlotIndex = targetSlotIndex,
                AllowPartial = allowPartialPickup,
                CustomData = InventoryCustomDataEntry.CloneArray(customData),
                SourceModule = nameof(InventoryPickupInteractable)
            };
        }

        private string BuildDisplayName()
        {
            var baseName = string.IsNullOrWhiteSpace(displayName) ? itemId : displayName;
            if (!appendCountToDisplayName || count <= 1)
            {
                return baseName;
            }

            return $"{baseName} x{count}";
        }

        private string BuildPromptText()
        {
            var text = string.IsNullOrWhiteSpace(promptText) ? "拾取" : promptText;
            if (IsLongOnly() && !text.Contains("长按"))
            {
                return "长按" + text;
            }

            return text;
        }

        private bool HasValidSupportedKind()
        {
            if (supportedKinds == InteractKind.None)
            {
                return false;
            }

            if (IsLongOnly() && longPressDuration <= 0f)
            {
                return false;
            }

            return true;
        }

        private bool IsLongOnly()
        {
            return (supportedKinds & InteractKind.Long) == InteractKind.Long
                   && (supportedKinds & InteractKind.Short) != InteractKind.Short;
        }

        private bool ResolveInventoryController(bool logMissing)
        {
            if (inventoryController != null)
            {
                return true;
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
                Debug.LogWarning("[InventoryPickupInteractable] 未找到 NiumaInventoryController，请在 Inspector 中绑定背包控制器。", this);
            }

            return inventoryController != null;
        }

        private static bool HasAcceptedItems(InventoryOperationResult result)
        {
            return HasAny(result?.AddedItems) || HasAny(result?.ChangedItems);
        }

        private static bool HasAny(InventoryItemSnapshot[] items)
        {
            return items != null && items.Length > 0;
        }

        private int GetOverflowCount(InventoryOperationResult result)
        {
            var overflowItems = result.OverflowItems;
            if (overflowItems == null || overflowItems.Length == 0)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < overflowItems.Length; i++)
            {
                var item = overflowItems[i];
                if (item != null && string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    total += Math.Max(0, item.Count);
                }
            }

            return total;
        }

        private void LogPickupFailed(InventoryOperationResult result)
        {
            if (!logWarnings)
            {
                return;
            }

            Debug.LogWarning(
                $"[InventoryPickupInteractable] 拾取失败：ItemId={itemId}, Count={count}, Reason={result?.Reason.ToString() ?? "<null>"}, Message={result?.Message ?? "<null>"}。",
                this);
        }

        private void LogInconsistentSuccess(InventoryOperationResult result)
        {
            if (!logWarnings)
            {
                return;
            }

            Debug.LogWarning(
                $"[InventoryPickupInteractable] AddItem 返回成功但没有 AddedItems 或 ChangedItems。为避免场景物误消失，本次不隐藏拾取物：ItemId={itemId}, Count={count}, Message={result?.Message ?? "<null>"}。",
                this);
        }

        private void HideAfterPickup()
        {
            DisableConfiguredColliders();

            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }

            if (destroyGameObjectOnPickup)
            {
                Destroy(gameObject);
                return;
            }

            if (deactivateGameObjectOnPickup)
            {
                gameObject.SetActive(false);
            }
        }

        private void DisableConfiguredColliders()
        {
            if (collidersToDisable != null && collidersToDisable.Length > 0)
            {
                for (var i = 0; i < collidersToDisable.Length; i++)
                {
                    if (collidersToDisable[i] != null)
                    {
                        collidersToDisable[i].enabled = false;
                    }
                }

                return;
            }

            var ownCollider = GetComponent<Collider>();
            if (ownCollider != null)
            {
                ownCollider.enabled = false;
            }
        }

        private void OnValidate()
        {
            count = count > 0 ? count : 1;
            targetSlotIndex = targetSlotIndex >= 0 ? targetSlotIndex : -1;
            priority = priority > 0f ? priority : 0f;
            longPressDuration = longPressDuration > 0f ? longPressDuration : 0f;
        }
    }
}
