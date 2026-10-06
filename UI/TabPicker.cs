using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YKF;

namespace SpecificPortraits.UI
{
    public class TabPicker : YKLayout<object>
    {
        UIText selectedCharaText = null;
        Chara selectedChara = null;
        Image selectedCharaImage = null;

        public override void OnLayout()
        {
            YKGrid gridLayoutRerollButton = Grid();
            gridLayoutRerollButton.Layout.cellSize = new Vector2(400f, 40f);
            gridLayoutRerollButton.Layout.childAlignment = TextAnchor.MiddleLeft;

            gridLayoutRerollButton.Button("マップ全体を再抽選する"._("Reroll Entire Map"), () =>
            {
                foreach (var chara in ELayer._map.charas.Where(c => !c.IsPC).ToList())
                {
                    PortraitPatch.SetPortraitPrefix(chara, chara.bio);
                    var igo = GameObject.Find($"{chara.GetHashCode()} IGO");
                    if(igo != null)
                    {
                        var image = igo.GetComponent<Image>();
                        image.sprite = !string.IsNullOrEmpty(chara.c_idPortrait) && Portrait.modPortraits.dict.ContainsKey(chara.c_idPortrait) ? Portrait.modPortraits.dict[chara.c_idPortrait].GetObject() : chara.GetSprite();
                    }
                }
            });
            gridLayoutRerollButton.Button("選択を再ロール"._("Reroll Selection"), () =>
            {
                if(selectedChara != null)
                {
                    PortraitPatch.SetPortraitPrefix(selectedChara, selectedChara.bio);
                    var igo = GameObject.Find($"{selectedChara.GetHashCode()} IGO");
                    if (igo != null)
                    {
                        var image = igo.GetComponent<Image>();
                        image.sprite = !string.IsNullOrEmpty(selectedChara.c_idPortrait) && Portrait.modPortraits.dict.ContainsKey(selectedChara.c_idPortrait) ? Portrait.modPortraits.dict[selectedChara.c_idPortrait].GetObject() : selectedChara.GetSprite();
                    }
                }
            });
            YKGrid gridLayoutMain = Grid();
            //gridLayoutMain.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 700f);
            gridLayoutMain.Layout.cellSize = new Vector2(700f, 700f);
            #region original picker
            YKScroll scrollLayoutOriginal = gridLayoutMain.Scroll();
            //scrollLayoutOriginal.headerRect.SetActive(false);
            //scrollLayoutOriginal.Layout.spacing = 10f;

            selectedCharaText = scrollLayoutOriginal.Text("ポートレート交換のためにユニークでないキャラクターを選んでください"._("Please select a non-unique character for portrait swap"));
            YKGrid gridLayout = scrollLayoutOriginal.Grid();
            gridLayout.transform.SetParent(scrollLayoutOriginal.ContentTransform);
            //gridLayout.transform.SetParent(scrollLayoutOriginal.ContentTransform);
            //gridLayout.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 160f);
            gridLayout.Layout.cellSize = new Vector2(160f, 220f);

            var allNonUniqueCharas = ELayer._map.charas.ToList();
            for (int i = 0; i < allNonUniqueCharas.Count; i++)
            {
                var chara = allNonUniqueCharas[i];

                var charaCardGO = new GameObject();
                var vlg = charaCardGO.AddComponent<VerticalLayoutGroup>();
                charaCardGO.transform.SetParent(gridLayout.transform);
                if (!chara.GetIdPortrait().IsEmpty() || chara.GetSprite() != null)
                {
                    try
                    {
                        var imageGO = new GameObject($"{chara.GetHashCode()} IGO");
                        imageGO.transform.SetParent(charaCardGO.transform);
                        var image = imageGO.AddComponent<Image>();
                        image.sprite = !chara.GetIdPortrait().IsEmpty() && Portrait.modPortraits.dict.ContainsKey(chara.GetIdPortrait()) ? Portrait.modPortraits.dict[chara.GetIdPortrait()].GetObject() : chara.GetSprite();
                        var button = imageGO.AddComponent<Button>();
                        button.onClick.AddListener(() => { 
                            if (selectedCharaText != null) {
                                if(selectedCharaImage != null)
                                    selectedCharaImage.color = new Color(1f, 1f, 1f, 1f);

                                selectedCharaText.text = chara.Name;
                                selectedChara = chara;
                                selectedCharaImage = image;
                                image.color = new Color(0.6f,0.6f,.6f,1f);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                }
                else
                {

                }
                var charaCardButton = Button(chara.Name, () =>
                {
                    if (selectedCharaText != null)
                        selectedCharaText.text = chara.Name;
                });
                charaCardButton.transform.SetParent(charaCardGO.transform);
            }
            #endregion

            #region replacement picker

            YKScroll scrollLayoutReplacement = gridLayoutMain.Scroll();
            //scrollLayoutReplacement.headerRect.SetActive(false);
            //scrollLayoutReplacement.Layout.spacing = 10f;

            YKGrid gridLayoutReplacement = scrollLayoutReplacement.Grid();
            gridLayoutReplacement.transform.SetParent(scrollLayoutReplacement.ContentTransform);
            //gridLayoutReplacement.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 160f);
            gridLayoutReplacement.Layout.cellSize = new Vector2(160f, 220f);

            var charaResetCardGO = new GameObject();
            //var vlg = charaResetCardGO.AddComponent<VerticalLayoutGroup>();
            charaResetCardGO.transform.SetParent(gridLayoutReplacement.transform);
            gridLayoutReplacement.Button("ポートレートスプライトをリセットする"._("RESET PORTRAIT SPRITE"), () =>
            {
                if (selectedChara != null)
                {
                    selectedChara.c_idPortrait = null;
                    selectedCharaImage.sprite = selectedChara.GetSprite();
                }
                    
            });

            for (int i = 0; i < Portrait.modPortraits.list.Count; i++)
            {
                var sprite = Portrait.modPortraits.list[i];

                var charaCardGO = new GameObject();
                var vlg = charaCardGO.AddComponent<VerticalLayoutGroup>();
                charaCardGO.transform.SetParent(gridLayoutReplacement.transform);

                if (sprite.id != null && Portrait.modPortraits.dict.ContainsKey(sprite.id))
                {
                    try
                    {
                        var imageGO = new GameObject();
                        imageGO.transform.SetParent(charaCardGO.transform);
                        var image = imageGO.AddComponent<Image>();
                        image.sprite = SpecificPortraits.spriteListData[sprite.id];

                        var button = imageGO.AddComponent<Button>();
                        button.onClick.AddListener(() =>
                        {
                            if (selectedChara != null)
                            {
                                selectedChara.c_idPortrait = sprite.id;
                                selectedCharaImage.sprite = SpecificPortraits.spriteListData[sprite.id];
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
            #endregion
            ELayer.debug.enable = false;
            ELayer.player.flags.debugEnabled = false;
        }
    }
}
