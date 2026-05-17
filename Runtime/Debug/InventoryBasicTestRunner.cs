using System;
using System.Collections.Generic;
using System.Linq;
using NiumaInventory.Config;
using NiumaInventory.Data;
using NiumaInventory.Enum;
using NiumaInventory.Request;
using NiumaInventory.Service;
using UnityEngine;

namespace NiumaInventory.Debugging
{
    /// <summary>
    /// 背包模块基础测试入口。
    /// 该组件只用于开发阶段在 Unity 场景内手动验证核心流程，不参与正式业务。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryBasicTestRunner : MonoBehaviour
    {
        [Header("测试行为")]
        [Tooltip("运行测试后是否在 Console 输出每一步通过信息。关闭后只输出最终结果和失败原因。")]
        [SerializeField] private bool verboseLog = true;

        [ContextMenu("NiumaInventoryTest/运行基础测试")]
        public void RunBasicTests()
        {
            var createdAssets = new List<ScriptableObject>();
            var failures = new List<string>();

            try
            {
                var definitions = CreateTestDefinitions(createdAssets);
                var containers = CreateTestContainers(createdAssets);
                var service = new InventoryService(definitions, containers);

                TestAddSplitMerge(service, failures);
                TestMoveLockUniqueAndUse(service, failures);
                TestSortContainer(definitions, containers, failures);
                TestExportImport(service, definitions, containers, failures);

                if (failures.Count == 0)
                {
                    Debug.Log("[NiumaInventoryTest] 基础测试通过：添加、拆分、合并、移动、锁定、排序、唯一物品、使用、导出导入均正常。", this);
                    return;
                }

                Debug.LogError("[NiumaInventoryTest] 基础测试失败：\n" + string.Join("\n", failures), this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NiumaInventoryTest] 基础测试发生异常：{ex}", this);
            }
            finally
            {
                ReleaseCreatedAssets(createdAssets);
            }
        }

        private void TestAddSplitMerge(InventoryService service, List<string> failures)
        {
            var addResult = service.AddItem(new AddItemRequest
            {
                ItemId = "test_herb",
                Count = 7,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectSuccess("添加 7 个材料", addResult, failures);
            ExpectEqual("材料总数", 7, service.GetItemCount("test_herb"), failures);

            var firstStack = FindItem(service, "test_herb", item => item.Count >= 5);
            if (firstStack == null)
            {
                failures.Add("未找到可拆分的 test_herb 堆叠。");
                return;
            }

            var splitResult = service.SplitStack(new SplitStackRequest
            {
                SourceInstanceId = firstStack.InstanceId,
                SplitCount = 2,
                TargetContainerId = "main",
                TargetSlotIndex = 4,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectSuccess("拆分材料堆叠", splitResult, failures);

            var splitStack = splitResult.AddedItems?.FirstOrDefault();
            if (splitStack == null)
            {
                failures.Add("拆分成功但没有返回新堆 AddedItems。");
                return;
            }

            ExpectTrue("拆分生成新实例 ID", !string.Equals(firstStack.InstanceId, splitStack.InstanceId, StringComparison.Ordinal), failures);
            ExpectEqual("拆分后材料总数", 7, service.GetItemCount("test_herb"), failures);

            var mergeResult = service.MergeStack(new MergeStackRequest
            {
                SourceInstanceId = splitStack.InstanceId,
                TargetInstanceId = firstStack.InstanceId,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectSuccess("合并材料堆叠", mergeResult, failures);
            ExpectEqual("合并后材料总数", 7, service.GetItemCount("test_herb"), failures);

            LogStep("添加、拆分、合并测试完成。");
        }

        private void TestMoveLockUniqueAndUse(InventoryService service, List<string> failures)
        {
            var addPotionResult = service.AddItem(new AddItemRequest
            {
                ItemId = "test_potion",
                Count = 2,
                TargetContainerId = "main",
                TargetSlotIndex = 6,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectSuccess("添加消耗品", addPotionResult, failures);

            var potion = addPotionResult.AddedItems?.FirstOrDefault();
            if (potion == null)
            {
                failures.Add("添加消耗品成功但没有返回 AddedItems。");
                return;
            }

            var moveResult = service.MoveItem(new MoveItemRequest
            {
                InstanceId = potion.InstanceId,
                TargetContainerId = "main",
                TargetSlotIndex = 7,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectSuccess("移动消耗品到空格", moveResult, failures);

            var movedPotion = FindItem(service, potion.InstanceId);
            ExpectEqual("移动后格子索引", 7, movedPotion?.SlotIndex ?? -1, failures);

            var lockResult = service.LockItem(potion.InstanceId);
            ExpectSuccess("锁定消耗品", lockResult, failures);

            var lockedMoveResult = service.MoveItem(new MoveItemRequest
            {
                InstanceId = potion.InstanceId,
                TargetContainerId = "main",
                TargetSlotIndex = 5,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectTrue("锁定物品不能移动", lockedMoveResult != null && !lockedMoveResult.Succeeded && lockedMoveResult.Reason == InventoryFailureReason.ItemLocked, failures);

            var unlockResult = service.UnlockItem(potion.InstanceId);
            ExpectSuccess("解锁消耗品", unlockResult, failures);

            var useResult = service.UseItem(new UseItemRequest
            {
                InstanceId = potion.InstanceId,
                Count = 1,
                SourceModule = nameof(InventoryBasicTestRunner),
                ContextId = "basic_test"
            });
            ExpectSuccess("使用 1 个消耗品", useResult, failures);
            ExpectEqual("使用后消耗品数量", 1, service.GetItemCount("test_potion"), failures);

            var addUniqueResult = service.AddItem(new AddItemRequest
            {
                ItemId = "test_family_token",
                Count = 1,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectSuccess("添加唯一物品", addUniqueResult, failures);

            var duplicateUniqueResult = service.AddItem(new AddItemRequest
            {
                ItemId = "test_family_token",
                Count = 1,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectTrue("唯一物品不能重复添加", duplicateUniqueResult != null && !duplicateUniqueResult.Succeeded && duplicateUniqueResult.Reason == InventoryFailureReason.UniqueItemAlreadyOwned, failures);

            LogStep("移动、锁定、唯一物品、使用测试完成。");
        }

        private void TestSortContainer(ItemDefinition[] definitions, InventoryContainerConfig[] containers, List<string> failures)
        {
            var service = new InventoryService(definitions, containers);
            ExpectSuccess("排序测试添加消耗品", service.AddItem(new AddItemRequest
            {
                ItemId = "test_potion",
                Count = 1,
                TargetContainerId = "main",
                TargetSlotIndex = 0,
                SourceModule = nameof(InventoryBasicTestRunner)
            }), failures);
            ExpectSuccess("排序测试添加材料", service.AddItem(new AddItemRequest
            {
                ItemId = "test_herb",
                Count = 1,
                TargetContainerId = "main",
                TargetSlotIndex = 2,
                SourceModule = nameof(InventoryBasicTestRunner)
            }), failures);
            ExpectSuccess("排序测试添加唯一物品", service.AddItem(new AddItemRequest
            {
                ItemId = "test_family_token",
                Count = 1,
                TargetContainerId = "main",
                TargetSlotIndex = 4,
                SourceModule = nameof(InventoryBasicTestRunner)
            }), failures);

            var herb = FindItem(service, "test_herb", item => item.Count == 1);
            if (herb == null)
            {
                failures.Add("排序测试未找到需要锁定的 test_herb。");
                return;
            }

            ExpectSuccess("排序测试锁定材料", service.LockItem(herb.InstanceId), failures);
            var revisionBeforeSort = service.Revision;

            var sortResult = service.SortContainer(new SortContainerRequest
            {
                ContainerId = "main",
                SortKeys = new[] { InventorySortKey.ItemId },
                KeepLockedSlot = true,
                SourceModule = nameof(InventoryBasicTestRunner)
            });
            ExpectSuccess("按 ItemId 整理容器", sortResult, failures);

            var familyToken = FindItem(service, "test_family_token", item => item.Count == 1);
            var potion = FindItem(service, "test_potion", item => item.Count == 1);
            var lockedHerb = FindItem(service, herb.InstanceId);

            ExpectEqual("排序后唯一物品进入第一个可用格", 0, familyToken?.SlotIndex ?? -1, failures);
            ExpectEqual("排序后消耗品进入第二个可用格", 1, potion?.SlotIndex ?? -1, failures);
            ExpectEqual("排序时锁定材料保持原格子", 2, lockedHerb?.SlotIndex ?? -1, failures);
            ExpectTrue("排序后材料仍保持锁定", lockedHerb != null && lockedHerb.IsLocked, failures);
            ExpectEqual("排序成功后 Revision 递增", revisionBeforeSort + 1, service.Revision, failures);

            LogStep("容器整理排序测试完成。");
        }

        private void TestExportImport(InventoryService source, ItemDefinition[] definitions, InventoryContainerConfig[] containers, List<string> failures)
        {
            var snapshot = source.ExportSnapshot();
            ExpectTrue("导出快照不为空", snapshot != null, failures);
            ExpectTrue("导出物品数量大于 0", snapshot?.Items != null && snapshot.Items.Length > 0, failures);

            var restored = new InventoryService(definitions, containers);
            restored.ImportSnapshot(snapshot);

            ExpectEqual("读档后材料数量", source.GetItemCount("test_herb"), restored.GetItemCount("test_herb"), failures);
            ExpectEqual("读档后消耗品数量", source.GetItemCount("test_potion"), restored.GetItemCount("test_potion"), failures);
            ExpectEqual("读档后唯一物品数量", source.GetItemCount("test_family_token"), restored.GetItemCount("test_family_token"), failures);
            ExpectEqual("读档后 Revision", snapshot.Revision, restored.Revision, failures);

            LogStep("导出导入测试完成。");
        }

        private static ItemDefinition[] CreateTestDefinitions(List<ScriptableObject> createdAssets)
        {
            return new[]
            {
                CreateItem(createdAssets, "test_herb", "测试草药", ItemType.Material, 5, false, true),
                CreateItem(createdAssets, "test_potion", "测试药水", ItemType.Consumable, 10, true, true),
                CreateItem(createdAssets, "test_family_token", "测试姓氏信物", ItemType.KeyItem, 99, false, true, true)
            };
        }

        private static InventoryContainerConfig[] CreateTestContainers(List<ScriptableObject> createdAssets)
        {
            var main = ScriptableObject.CreateInstance<InventoryContainerConfig>();
            main.ContainerId = "main";
            main.DisplayName = "测试主背包";
            main.ContainerType = InventoryContainerType.Main;
            main.SlotCount = 8;
            main.MaxWeight = 0f;
            main.AllowAutoStack = true;
            main.AllowManualMove = true;
            main.IsUnlockedByDefault = true;
            createdAssets.Add(main);
            return new[] { main };
        }

        private static ItemDefinition CreateItem(
            List<ScriptableObject> createdAssets,
            string itemId,
            string displayName,
            ItemType itemType,
            int maxStackCount,
            bool canUse,
            bool canMove,
            bool isUnique = false)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.ItemId = itemId;
            item.DisplayName = displayName;
            item.ItemType = itemType;
            item.Quality = ItemQuality.Common;
            item.MaxStackCount = maxStackCount;
            item.CanUse = canUse;
            item.CanMove = canMove;
            item.IsUnique = isUnique;
            createdAssets.Add(item);
            return item;
        }

        private static InventoryItemSnapshot FindItem(InventoryService service, string instanceId)
        {
            return service.ExportSnapshot().Items.FirstOrDefault(item => string.Equals(item.InstanceId, instanceId, StringComparison.Ordinal));
        }

        private static InventoryItemSnapshot FindItem(InventoryService service, string itemId, Func<InventoryItemSnapshot, bool> predicate)
        {
            return service.ExportSnapshot().Items.FirstOrDefault(item =>
                string.Equals(item.ItemId, itemId, StringComparison.Ordinal) && predicate(item));
        }

        private void ExpectSuccess(string label, InventoryOperationResult result, List<string> failures)
        {
            if (result != null && result.Succeeded)
            {
                LogStep($"{label}：通过");
                return;
            }

            failures.Add($"{label}：失败，Reason={result?.Reason.ToString() ?? "<null>"}，Message={result?.Message ?? "<null>"}");
        }

        private void ExpectTrue(string label, bool condition, List<string> failures)
        {
            if (condition)
            {
                LogStep($"{label}：通过");
                return;
            }

            failures.Add($"{label}：失败");
        }

        private void ExpectEqual<T>(string label, T expected, T actual, List<string> failures)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
            {
                LogStep($"{label}：通过");
                return;
            }

            failures.Add($"{label}：失败，expected={expected}，actual={actual}");
        }

        private void LogStep(string message)
        {
            if (verboseLog)
            {
                Debug.Log($"[NiumaInventoryTest] {message}", this);
            }
        }

        private static void ReleaseCreatedAssets(List<ScriptableObject> createdAssets)
        {
            for (var i = 0; i < createdAssets.Count; i++)
            {
                var asset = createdAssets[i];
                if (asset == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(asset);
                }
                else
                {
                    DestroyImmediate(asset);
                }
            }
        }
    }
}
