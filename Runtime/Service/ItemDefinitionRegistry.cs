using System;
using System.Collections.Generic;
using NiumaInventory.Config;
using UnityEngine;

namespace NiumaInventory.Service
{
    /// <summary>
    /// 物品定义注册表。
    /// 负责用稳定 ItemId 建立静态配置索引，不保存玩家运行时数据。
    /// </summary>
    public sealed class ItemDefinitionRegistry
    {
        private readonly Dictionary<string, ItemDefinition> _definitions =
            new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

        /// <summary>
        /// 当前已注册的物品定义数量。
        /// </summary>
        public int Count => _definitions.Count;

        public ItemDefinitionRegistry(IEnumerable<ItemDefinition> definitions = null)
        {
            SetDefinitions(definitions);
        }

        /// <summary>
        /// 重建物品定义索引。
        /// ItemId 为空的配置会被跳过；重复 ItemId 后注册的配置会覆盖先注册的配置。
        /// </summary>
        public void SetDefinitions(IEnumerable<ItemDefinition> definitions)
        {
            _definitions.Clear();
            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    continue;
                }

                if (_definitions.ContainsKey(definition.ItemId))
                {
                    Debug.LogWarning($"[NiumaInventory] 发现重复 ItemId：{definition.ItemId}，后注册的 ItemDefinition 会覆盖先注册的配置。");
                }

                _definitions[definition.ItemId] = definition;
            }
        }

        /// <summary>
        /// 尝试获取物品定义。
        /// </summary>
        public bool TryGetDefinition(string itemId, out ItemDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(itemId)
                   && _definitions.TryGetValue(itemId, out definition)
                   && definition != null;
        }

        /// <summary>
        /// 是否存在指定物品定义。
        /// </summary>
        public bool Contains(string itemId)
        {
            return TryGetDefinition(itemId, out _);
        }
    }
}
