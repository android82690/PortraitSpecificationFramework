using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YKF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SpecificPortraits.UI
{
    public class ViewPortraitSelector 
    {
        public List<ModItem<Sprite>> SelectedCards { get; set; } = new List<ModItem<Sprite>>();
        //UIDropdown filterDropdownMod;
        public int CurrentMod { get; set; } = 0;
        public int CurrentLocation { get; set; } = 0;
        YKLayout _parentLayout;
        UIDropdown _modFilter;
        //Dictionary<string, List<string>> PoolsByPortrait = new Dictionary<string, List<string>>();
        Dictionary<string, List<PortraitPoolTag>> PoolsByPortrait = new Dictionary<string, List<PortraitPoolTag>>();

        public ViewPortraitSelector(YKLayout parentLayout, UIDropdown modFilter, YKLayout parentTransform = null) {
            if (parentTransform == null)
                parentTransform = parentLayout;

            _parentLayout = parentLayout;
            _modFilter = modFilter;

            YKScroll scrollLayoutPortraitList = parentTransform.Scroll();
            //scrollLayoutPortraitList.headerRect.SetActive(false);
            scrollLayoutPortraitList.Layout.spacing = 10f;

            YKGrid gridLayoutSpriteList = scrollLayoutPortraitList.Grid();
            gridLayoutSpriteList.transform.SetParent(scrollLayoutPortraitList.ContentTransform);
            //gridLayoutSpriteList.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 160f);
            gridLayoutSpriteList.Layout.cellSize = new Vector2(150f, 205f);
            gridLayoutSpriteList.Layout.constraintCount = 4;
            //attach dropdown listener
            _modFilter.onValueChanged.AddListener((int index) => {
                gridLayoutSpriteList.transform.DestroyAllChildren();
                PoolsByPortrait = GetPoolTags();
                GenerateSpriteListAndAttachToTransform(gridLayoutSpriteList.transform, SpecificPortraits.loadedModPackages[index].dirInfo.Name);
            });
            PoolsByPortrait = GetPoolTags();
            GenerateSpriteListAndAttachToTransform(gridLayoutSpriteList.transform, "_Elona");
        }

        public void RefreshPoolList()
        {
            Console.WriteLine("Refreshing Pool List");
            Console.WriteLine($"Parent Layout is: {_parentLayout.GetType()}: {_parentLayout is TabPools}");
            if (_parentLayout is TabPools) (_parentLayout as TabPools).UpdatePoolList(GetCommonPoolsFromSelection());
        }
        public void RemovePool(string pool)
        {
            foreach(var card in SelectedCards)
            {
                if (PoolsByPortrait.ContainsKey(card.id))
                {
                    foreach(var tag in PoolsByPortrait[card.id])
                    {
                        if (SpecificPortraits.PortraitPoolTags.ContainsKey(tag.GetHashCode()))
                        {
                            SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.Remove(card.id);
                            if(SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.Count() == 0)
                            {
                                SpecificPortraits.PortraitPoolTags.Remove(tag.GetHashCode());
                            }
                        }
                    }
                    PoolsByPortrait[card.id].RemoveAll(p => p.ToString() == pool);
                }
            }
        }

        public void SavePoolFile()
        {
            var poolFile = new PortraitPoolFile();

            poolFile.Version = 3;
            poolFile.Tags = SpecificPortraits.PortraitPoolTags.Where(t => t.Value.ModID == SpecificPortraits.loadedModPackages[CurrentMod].dirInfo.Name).Select(t => t.Value).ToList();

            var jsonText = JsonConvert.SerializeObject(poolFile);
                if (_modFilter.value != 0)
                    File.WriteAllText(Path.Combine(SpecificPortraits.loadedModPackages[_modFilter.value].dirInfo.FullName, "PSFLinks.json"), jsonText);
                else //vanilla saves to this mod
                    File.WriteAllText(Path.Combine(ModManager.DirWorkshop.FullName, "3369998688", "PSFLinks.json"), jsonText);
            
        }

        public Dictionary<string, List<PortraitPoolTag>> GetPoolTags()
        {
            var result = new Dictionary<string, List<PortraitPoolTag>>();
            foreach (var tag in SpecificPortraits.PortraitPoolTags.Where(t => t.Value.ModID == SpecificPortraits.loadedModPackages[CurrentMod].dirInfo.Name).Select(t => t.Value))
            {
                foreach (var portrait in tag.Portraits)
                {
                    if (!result.ContainsKey(portrait))
                        result[portrait] = new List<PortraitPoolTag>();
                    result[portrait].Add(tag);
                }
            }

            foreach (var portrait in result.Keys.ToList())
            {
                result[portrait] = result[portrait].Distinct().ToList();
            }

            return result;
        }

        public void GenerateSpriteListAndAttachToTransform(Transform parentTransform, string modId)
        {
            try
            {
                SelectedCards = new List<ModItem<Sprite>>();
                RefreshPoolList();
                //link mod, without portraits
                List<ModItem<Sprite>> spriteList = new List<ModItem<Sprite>>();
                if (!Directory.Exists(Path.Combine(SpecificPortraits.loadedModPackages[CurrentMod].dirInfo.FullName, "Portrait")))
                {
                    spriteList = Portrait.modPortraits.list;
                }
                //mod with portraits
                else if (SpecificPortraits.spriteListDataByMod.ContainsKey(modId))
                {
                    spriteList = SpecificPortraits.spriteListDataByMod[modId].list;
                }
                    
                foreach (var sprite in spriteList)
                {

                    var charaCardGO = new GameObject();
                    var vlg = charaCardGO.AddComponent<VerticalLayoutGroup>();
                    charaCardGO.transform.SetParent(parentTransform);

                    if (sprite.id != null && SpecificPortraits.spriteListData.ContainsKey(sprite.id))
                    {
                        try
                        {
                            var imageGO = new GameObject($"{sprite.id} PSV IGO");
                            imageGO.transform.SetParent(charaCardGO.transform);
                            var image = imageGO.AddComponent<Image>();
                            image.sprite = SpecificPortraits.spriteListData[sprite.id];

                            var button = imageGO.AddComponent<Button>();
                            button.onClick.AddListener(() =>
                            {
                                if (!SelectedCards.Contains(sprite))
                                {
                                    image.color = new Color(1f, .6f, 1f);
                                    SelectedCards.Add(sprite);
                                    RefreshPoolList();
                                }
                                else
                                {
                                    image.color = new Color(1f, 1f, 1f);
                                    SelectedCards.Remove(sprite);
                                    RefreshPoolList();
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            Console.WriteLine(ex.StackTrace);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
                Console.WriteLine($"{ex.StackTrace}");
            }
        }

        public List<string> GetCommonPoolsFromSelection()
        {
            List<string> shortestCommonList = new List<string>();
            foreach ( var selectedCard in SelectedCards)
            {
                if(PoolsByPortrait.ContainsKey(selectedCard.id))
                    shortestCommonList.AddRange(PoolsByPortrait[selectedCard.id].Select(tag => tag.ToString()));
            }

            shortestCommonList = shortestCommonList
                .GroupBy(c => c)
                .Where(g => g.Count() == SelectedCards.Count)
                .Select(g => g.Key)
                .ToList();
            return shortestCommonList;
        }

        public void AddPortraitsToPool(PortraitPoolTag tag)
        {
            try
            {
                foreach (var card in SelectedCards)
                {
                    if (!PoolsByPortrait.ContainsKey(card.id))
                    {
                       PoolsByPortrait[card.id] = new List<PortraitPoolTag>();
                    } else if(PoolsByPortrait[card.id].Any(t => t.GetHashCode() == tag.GetHashCode()))
                    {
                        continue;
                    }
                    PoolsByPortrait[card.id].Add(tag);
                }

                if (!SpecificPortraits.PortraitPoolTags.ContainsKey(tag.GetHashCode()))
                {
                    tag.Portraits = SelectedCards.Select(c => c.id).ToList();
                    SpecificPortraits.PortraitPoolTags.Add(tag.GetHashCode(), tag);
                } else
                {
                    SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.AddRange(SelectedCards.Select(c => c.id).ToList());
                    SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits = SpecificPortraits.PortraitPoolTags[tag.GetHashCode()].Portraits.Distinct().ToList();
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"[PortraitSpecificationFramework] ERROR: {ex.Message}");
            }
            
        }
    }
}
