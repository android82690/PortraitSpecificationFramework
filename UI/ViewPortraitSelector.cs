using YKF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SpecificPortraits.UI
{
    /// <summary>
    /// Portrait grid for one package with multi-select, plus the pool bookkeeping
    /// behind the pool editor tab.
    /// </summary>
    public class ViewPortraitSelector
    {
        public List<ModItem<Sprite>> SelectedCards { get; set; } = new List<ModItem<Sprite>>();
        public int CurrentMod { get; set; } = 0;
        /// <summary>Called with the portrait id under the pointer, or an empty string when it leaves.</summary>
        public Action<string> OnHover { get; set; }

        readonly YKLayout _parentLayout;
        readonly UIDropdown _modFilter;
        readonly Dictionary<string, Image> cardImages = new Dictionary<string, Image>();
        readonly List<KeyValuePair<GameObject, string>> cards = new List<KeyValuePair<GameObject, string>>();
        string filterText = "";
        Dictionary<string, List<PortraitPoolTag>> PoolsByPortrait = new Dictionary<string, List<PortraitPoolTag>>();

        static readonly Color selectedColor = new Color(1f, 0.6f, 1f);

        public BaseModPackage CurrentPackage => SpecificPortraits.loadedModPackages[CurrentMod];

        public ViewPortraitSelector(YKLayout parentLayout, UIDropdown modFilter, YKLayout parentTransform = null)
        {
            if (parentTransform == null)
                parentTransform = parentLayout;

            _parentLayout = parentLayout;
            _modFilter = modFilter;

            YKScroll scrollLayoutPortraitList = parentTransform.Scroll();
            scrollLayoutPortraitList.Layout.spacing = 10f;

            YKGrid gridLayoutSpriteList = TabPicker.PortraitGrid(scrollLayoutPortraitList);

            _modFilter.onValueChanged.AddListener((int index) =>
            {
                CurrentMod = index;
                gridLayoutSpriteList.transform.DestroyAllChildren();
                PoolsByPortrait = GetPoolTags();
                GenerateSpriteListAndAttachToTransform(gridLayoutSpriteList.transform, CurrentPackage.dirInfo.Name);
            });
            PoolsByPortrait = GetPoolTags();
            GenerateSpriteListAndAttachToTransform(gridLayoutSpriteList.transform, CurrentPackage.dirInfo.Name);
        }

        public void RefreshPoolList()
        {
            if (_parentLayout is TabPools pools)
            {
                pools.UpdatePoolList(GetCommonPoolsFromSelection());
                pools.UpdateStatus();
            }
        }

        public void Filter(string text)
        {
            filterText = text ?? "";
            TabPicker.ApplyFilter(cards, filterText);
        }

        public void ClearSelection()
        {
            foreach (var card in SelectedCards)
            {
                if (cardImages.TryGetValue(card.id, out var image) && image != null)
                    image.color = Color.white;
            }
            SelectedCards.Clear();
            RefreshPoolList();
        }

        /// <summary>Remove the selected portraits from the pool with this label.</summary>
        public void RemovePool(string pool)
        {
            bool changed = false;
            foreach (var card in SelectedCards)
            {
                if (!PoolsByPortrait.TryGetValue(card.id, out var tags))
                    continue;

                foreach (var tag in tags.Where(t => t.ToString() == pool).ToList())
                {
                    if (SpecificPortraits.PortraitPoolTags.TryGetValue(tag.Key, out var existing))
                    {
                        existing.Portraits.Remove(card.id);
                        if (existing.Portraits.Count == 0)
                            SpecificPortraits.PortraitPoolTags.Remove(tag.Key);
                        changed = true;
                    }
                }
                tags.RemoveAll(t => t.ToString() == pool);
            }
            if (changed)
                LinkFiles.Dirty.Add(CurrentPackage.dirInfo.Name);
        }

        public void SavePoolFile()
        {
            var package = CurrentPackage;
            try
            {
                var path = LinkFiles.Save(package);
                Msg.SayRaw((Lang.isJP ? "ポートレートプールを保存しました: " : "Portrait pools saved: ") + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogError($"Could not save pools for {package.title}: {ex.Message}");
                Msg.SayRaw(Lang.isJP ? "ポートレートプールを保存できませんでした" : "Portrait pools could not be saved.");
            }
            RefreshPoolList();
        }

        public Dictionary<string, List<PortraitPoolTag>> GetPoolTags()
        {
            var result = new Dictionary<string, List<PortraitPoolTag>>();
            var modId = CurrentPackage.dirInfo.Name;
            foreach (var tag in SpecificPortraits.PortraitPoolTags.Values.Where(t => t.ModID == modId))
            {
                foreach (var portrait in tag.Portraits)
                {
                    if (!result.TryGetValue(portrait, out var list))
                        result[portrait] = list = new List<PortraitPoolTag>();
                    if (!list.Contains(tag))
                        list.Add(tag);
                }
            }
            return result;
        }

        public void GenerateSpriteListAndAttachToTransform(Transform parentTransform, string modId)
        {
            try
            {
                SelectedCards = new List<ModItem<Sprite>>();
                cardImages.Clear();
                cards.Clear();
                RefreshPoolList();

                // A package without its own portraits (link-only packs, the settings package) shows every portrait.
                List<ModItem<Sprite>> spriteList;
                if (!Directory.Exists(Path.Combine(CurrentPackage.dirInfo.FullName, "Portrait")))
                    spriteList = Portrait.modPortraits.list;
                else if (SpecificPortraits.spriteListDataByMod.TryGetValue(modId, out var own))
                    spriteList = own.list;
                else
                    spriteList = new List<ModItem<Sprite>>();

                var pending = new List<KeyValuePair<Image, string>>();
                foreach (var sprite in spriteList)
                {
                    if (sprite.id == null || !Portrait.Exists(sprite.id))
                        continue;

                    // One flat Image per cell: no wrapper, no per-cell layout group, so the grid stays cheap to rebuild.
                    var imageGO = new GameObject($"{sprite.id} PSV IGO", typeof(RectTransform));
                    imageGO.transform.SetParent(parentTransform, false);
                    cards.Add(new KeyValuePair<GameObject, string>(imageGO, sprite.id.ToLowerInvariant()));
                    var image = imageGO.AddComponent<Image>();
                    image.preserveAspect = true;
                    image.enabled = false;
                    cardImages[sprite.id] = image;
                    pending.Add(new KeyValuePair<Image, string>(image, sprite.id));

                    HoverLabel.Attach(imageGO, sprite.id, label => OnHover?.Invoke(label));

                    var button = imageGO.AddComponent<Button>();
                    button.onClick.AddListener(() =>
                    {
                        if (!SelectedCards.Contains(sprite))
                        {
                            image.color = selectedColor;
                            SelectedCards.Add(sprite);
                        }
                        else
                        {
                            image.color = Color.white;
                            SelectedCards.Remove(sprite);
                        }
                        RefreshPoolList();
                    });
                }
                if (filterText.Length > 0)
                    TabPicker.ApplyFilter(cards, filterText);
                SpriteBatchLoader.Start(_parentLayout, pending);
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogError($"Could not build the portrait list: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>Pools that every selected portrait belongs to.</summary>
        public List<string> GetCommonPoolsFromSelection()
        {
            var all = new List<string>();
            foreach (var selectedCard in SelectedCards)
            {
                if (PoolsByPortrait.TryGetValue(selectedCard.id, out var tags))
                    all.AddRange(tags.Select(tag => tag.ToString()));
            }

            return all
                .GroupBy(c => c)
                .Where(g => g.Count() == SelectedCards.Count)
                .Select(g => g.Key)
                .ToList();
        }

        public void AddPortraitsToPool(PortraitPoolTag tag)
        {
            try
            {
                if (SelectedCards.Count == 0)
                {
                    Msg.SayRaw(Lang.isJP ? "先にポートレートを選んでください。" : "Select portraits first.");
                    return;
                }
                var pool = LinkFiles.AddToPool(tag, SelectedCards.Select(c => c.id));
                foreach (var card in SelectedCards)
                {
                    if (!PoolsByPortrait.TryGetValue(card.id, out var tags))
                        PoolsByPortrait[card.id] = tags = new List<PortraitPoolTag>();
                    if (!tags.Contains(pool))
                        tags.Add(pool);
                }
                LinkFiles.Dirty.Add(tag.ModID);
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogError($"Could not add portraits to the pool: {ex.Message}");
            }
        }
    }
}
