using System;
using System.Text;
using NiumaInventory.Controller;
using NiumaInventory.Data;
using NiumaSave.Controller;
using NiumaSave.Data;
using NiumaSave.Provider;
using UnityEngine;

namespace NiumaInventory.SaveBridge
{
    /// <summary>
    /// NiumaInventory 存档桥接器。
    /// 负责把背包快照转换为 NiumaSave 的 Section 数据，并在读档时恢复到背包控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaInventorySaveAdapter : MonoBehaviour, ISaveDataProvider
    {
        private const string InventorySectionId = "inventory";
        private const string InventorySectionVersionV1 = "1";
        private const string CurrentInventorySectionVersion = InventorySectionVersionV1;
        private const string InventorySectionFormat = "json";

        [Header("模块引用")]
        [Tooltip("背包模块根控制器。请拖入场景中的 NiumaInventoryController，导出和导入背包数据都会通过它完成。")]
        [SerializeField] private NiumaInventoryController inventoryController;

        [Tooltip("存档模块根控制器。开启自动注册时，请拖入场景中的 NiumaSaveController。")]
        [SerializeField] private NiumaSaveController saveController;

        [Header("注册行为")]
        [Tooltip("启用组件时是否自动注册到 NiumaSaveController。正式场景建议开启，并确保 NiumaSaveController 更早初始化，或把本组件挂在存档控制器子物体下。")]
        [SerializeField] private bool registerOnEnable = true;

        [Tooltip("引用为空时是否自动在场景中查找对应组件。仅建议调试阶段开启；正式多场景或 DontDestroyOnLoad 场景必须手动绑定，避免找到错误实例。")]
        [SerializeField] private bool autoFindReferences = true;

        private bool _registeredToSaveController;

        /// <summary>
        /// 背包模块的稳定存档段 ID。
        /// </summary>
        public string SectionId => InventorySectionId;

        /// <summary>
        /// 背包存档段结构版本。
        /// </summary>
        public string SectionVersion => CurrentInventorySectionVersion;

        /// <summary>
        /// 背包数据修订号。
        /// NiumaSave 通过该值判断背包模块是否发生变化。
        /// </summary>
        public long Revision => inventoryController != null ? inventoryController.InventoryRevision : 0L;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            if (registerOnEnable)
            {
                RegisterToSaveController();
            }
        }

        private void OnDisable()
        {
            UnregisterFromSaveController();
        }

        /// <summary>
        /// 导出背包运行时快照为 NiumaSave Section。
        /// 通过 SaveDataProviderRegistry 批量导出时，外层会捕获导出异常并转为结构化失败结果。
        /// 若外部直接调用该方法，必须自行处理 InvalidOperationException，避免缺少引用时打断完整存档流程。
        /// </summary>
        public SaveSectionData ExportSection()
        {
            ResolveReferences(false);
            if (inventoryController == null)
            {
                throw new InvalidOperationException("NiumaInventorySaveAdapter 缺少 NiumaInventoryController，无法导出背包存档。");
            }

            var saveData = inventoryController.ExportSnapshot() ?? new InventorySaveData();
            var json = JsonUtility.ToJson(saveData);
            var bytes = Encoding.UTF8.GetBytes(json);

            return new SaveSectionData
            {
                SectionId = SectionId,
                SectionVersion = SectionVersion,
                Format = InventorySectionFormat,
                DataEncoding = SaveDataEncoding.Base64,
                EncodedData = Convert.ToBase64String(bytes)
            };
        }

        /// <summary>
        /// 从 NiumaSave Section 导入背包快照。
        /// </summary>
        public SaveSectionImportResult ImportSection(SaveSectionData section)
        {
            ResolveReferences(false);
            if (inventoryController == null)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.ConfigMissing,
                    "NiumaInventorySaveAdapter 缺少 NiumaInventoryController，无法导入背包存档。");
            }

            if (section == null)
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.NullSection, "背包存档段为空。");
            }

            if (!string.Equals(section.SectionId, SectionId, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.SectionIdMismatch,
                    $"背包存档段 ID 不匹配：expected={SectionId}, actual={section.SectionId}");
            }

            if (!string.Equals(section.DataEncoding, SaveDataEncoding.Base64, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"背包存档段编码不支持：{section.DataEncoding}");
            }

            if (string.IsNullOrWhiteSpace(section.EncodedData))
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "背包存档段数据为空。");
            }

            try
            {
                var readResult = TryReadInventorySaveData(section, out var saveData);
                if (!readResult.Succeeded)
                {
                    return readResult;
                }

                inventoryController.ImportSnapshot(saveData);
                return SaveSectionImportResult.Success();
            }
            catch (Exception ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"背包存档段解析失败：{ex.Message}");
            }
        }

        private static SaveSectionImportResult TryReadInventorySaveData(SaveSectionData section, out InventorySaveData saveData)
        {
            saveData = null;
            switch (section.SectionVersion)
            {
                case InventorySectionVersionV1:
                    return TryReadVersion1(section, out saveData);
                default:
                    return SaveSectionImportResult.Fail(
                        SaveSectionImportErrorCode.VersionUnsupported,
                        $"背包存档段版本不支持：{section.SectionVersion}");
            }
        }

        private static SaveSectionImportResult TryReadVersion1(SaveSectionData section, out InventorySaveData saveData)
        {
            saveData = null;
            var bytes = Convert.FromBase64String(section.EncodedData);
            var json = Encoding.UTF8.GetString(bytes);
            saveData = JsonUtility.FromJson<InventorySaveData>(json);
            return saveData != null
                ? SaveSectionImportResult.Success()
                : SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "背包存档段解析结果为空。");
        }

        [ContextMenu("NiumaInventorySave/注册到存档模块")]
        private void RegisterToSaveController()
        {
            if (_registeredToSaveController)
            {
                return;
            }

            ResolveReferences(true);
            if (saveController == null)
            {
                return;
            }

            var registered = saveController.RegisterProvider(this);
            _registeredToSaveController = registered;
            if (!registered)
            {
                Debug.LogWarning("[NiumaInventorySaveAdapter] 注册背包存档 Provider 失败。", this);
            }
        }

        [ContextMenu("NiumaInventorySave/从存档模块取消注册")]
        private void UnregisterFromSaveController()
        {
            ResolveReferences(false);
            if (_registeredToSaveController && saveController != null)
            {
                saveController.UnregisterProvider(SectionId);
            }

            _registeredToSaveController = false;
        }

        private void ResolveReferences(bool logMissing)
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (inventoryController == null)
            {
#if UNITY_2023_1_OR_NEWER
                inventoryController = FindFirstObjectByType<NiumaInventoryController>();
#else
                inventoryController = FindObjectOfType<NiumaInventoryController>();
#endif
            }

            if (saveController == null)
            {
#if UNITY_2023_1_OR_NEWER
                saveController = FindFirstObjectByType<NiumaSaveController>();
#else
                saveController = FindObjectOfType<NiumaSaveController>();
#endif
            }

            if (logMissing && inventoryController == null)
            {
                Debug.LogWarning("[NiumaInventorySaveAdapter] 未找到 NiumaInventoryController，请在 Inspector 中绑定。", this);
            }

            if (logMissing && saveController == null)
            {
                Debug.LogWarning("[NiumaInventorySaveAdapter] 未找到 NiumaSaveController，请在 Inspector 中绑定。", this);
            }
        }
    }
}
