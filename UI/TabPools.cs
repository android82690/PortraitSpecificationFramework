using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YKF;

namespace SpecificPortraits.UI
{
    public class TabPools : YKLayout<object>
    {

        public UIDropdown filterDropdownMod;
        //public UIDropdown filterDropdownSaveDir;

        UIDropdown filterDropdownType;
        UIDropdown filterDropdownGender;
        UIDropdown filterDropdownRace;
        UIDropdown filterDropdownJob;


        UIInputText fieldID;
        UIInputText fieldMinAge;
        UIInputText fieldMaxAge;
        YKGrid poolListGrid;
        ViewPortraitSelector portraitSelectorView;

        public void UpdatePoolList(List<string> pools)
        {

            if (poolListGrid != null)
                poolListGrid.transform.DestroyAllChildren();
            foreach (var pool in pools)
            {
                var poolButton = poolListGrid.Button(pool, () => { });
                poolButton.onClick.AddListener(() =>
                {
                    if (portraitSelectorView != null)
                    {
                        portraitSelectorView.RemovePool(pool);
                        portraitSelectorView.RefreshPoolList();
                    }
                });
            }
        }

        public override void OnLayout()
        {
            #region header
            var filterModLayout = Grid();
            filterModLayout.Layout.constraintCount = 5;
            filterModLayout.Layout.cellSize = new Vector2(250f, 40f);
            filterModLayout.Text("モッドフィルター"._("Mod Filter"));
            filterDropdownMod = filterModLayout.Dropdown();
            filterDropdownMod.ClearOptions();
            filterDropdownMod.AddOptions(SpecificPortraits.loadedModPackages.Select(package => new Dropdown.OptionData(package.title)).ToList());
            filterDropdownMod.onValueChanged.AddListener((int index) =>
            {
                portraitSelectorView.CurrentMod = index;
            });

            filterModLayout.Button("すべて選択解除"._("Deselect All"), () =>
            {
                foreach (var item in portraitSelectorView.SelectedCards.Select(c => c.id))
                {
                    var igo = GameObject.Find($"{item} PSV IGO");
                    igo.GetComponent<Image>().color = new Color(1f, 1f, 1f);
                }
                portraitSelectorView.SelectedCards.Clear();
                portraitSelectorView.RefreshPoolList();
            });

            #endregion

            YKGrid gridLayoutMain = Grid();
            //gridLayoutMain.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 700f);
            gridLayoutMain.Layout.cellSize = new Vector2(700f, 700f);
            gridLayoutMain.Layout.constraintCount = 2;

            #region portrait picker

            portraitSelectorView = new ViewPortraitSelector(this, filterDropdownMod, gridLayoutMain);

            #endregion

            #region selection
            var scrollLayoutLinkFiles = gridLayoutMain.Vertical();
            //scrollLayoutLinkFiles.headerRect.SetActive(false);
            //scrollLayoutLinkFiles.layout.spacing = 10f;
            var filterTypeLayout = scrollLayoutLinkFiles.Grid();
            filterTypeLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterTypeLayout.Text("タイプを選択する"._("Select Type"));
            filterDropdownType = filterTypeLayout.Dropdown();
            filterDropdownType.ClearOptions();
            filterDropdownType.AddOptions(new List<Dropdown.OptionData> { new Dropdown.OptionData(SpecificPortraits.allTextJP._(SpecificPortraits.allText)), new Dropdown.OptionData("c"), new Dropdown.OptionData("guard"), new Dropdown.OptionData("foxfolk"), new Dropdown.OptionData("special") });
            var filterGenderLayout = scrollLayoutLinkFiles.Grid();
            filterGenderLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterGenderLayout.Text("性別を選択する"._("Select Gender"));
            filterDropdownGender = filterGenderLayout.Dropdown();
            filterDropdownGender.ClearOptions();
            filterDropdownGender.AddOptions(new List<Dropdown.OptionData> { new Dropdown.OptionData(SpecificPortraits.allTextJP._(SpecificPortraits.allText)), new Dropdown.OptionData("f"), new Dropdown.OptionData("m") });

            var filterRaceLayout = scrollLayoutLinkFiles.Grid();
            filterRaceLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterRaceLayout.Text("種族を選択する"._("Select Race"));
            filterDropdownRace = filterRaceLayout.Dropdown();
            filterDropdownRace.ClearOptions();
            filterDropdownRace.AddOptions(new List<Dropdown.OptionData> { new Dropdown.OptionData(SpecificPortraits.allTextJP._(SpecificPortraits.allText)) });
            filterDropdownRace.AddOptions(EClass.sources.races.map.OrderBy(kv => kv.Value.id).Select(r => new Dropdown.OptionData(r.Value.id)).ToList());
            var filterIDLayout = scrollLayoutLinkFiles.Grid ();
            filterIDLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterIDLayout.Text("IDを入力する"._("Enter ID"));
            fieldID = filterIDLayout.InputText("");
            //fieldID.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200f);
            //fieldID.tra.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200f);
            filterIDLayout.Button("検索"._("Search"), () =>
            {
                Dialog.InputName("ID検索"._("ID Search"), "", (cancel, text_input) =>
                {
                    if (string.IsNullOrEmpty(text_input))
                    {
                        return;
                    }

                    var viableIDs = EClass.sources.cards.map.Where(kv => kv.Value.isChara && (kv.Value.id.Contains(text_input) || kv.Value.name.Contains(text_input) || kv.Value.name_JP.Contains(text_input))).Select(i => new { id = i.Value.id, name = (Lang.isJP ? i.Value.name_JP : i.Value.name) }).Take(10).ToList();

                    Dialog.Choice("ID検索"._("ID Search"), (d) =>
                    {
                        d.AddButton($"{SpecificPortraits.allTextJP._(SpecificPortraits.allText)}", () =>
                        {
                            fieldID.Text = "all";
                        });
                        foreach (var choice in viableIDs)
                        {
                            d.AddButton($"{choice.id}, {choice.name}", () =>
                            {
                                fieldID.Text = choice.id;
                            });
                        }
                    });

                });
            });
            var filterJobLayout = scrollLayoutLinkFiles.Grid();
            filterJobLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterJobLayout.Text("職業を選択する"._("Select Job"));
            filterDropdownJob = filterJobLayout.Dropdown();
            filterDropdownJob.ClearOptions();
            filterDropdownJob.AddOptions(new List<Dropdown.OptionData> { new Dropdown.OptionData(SpecificPortraits.allTextJP._(SpecificPortraits.allText)) });
            filterDropdownJob.AddOptions(EClass.sources.jobs.map.OrderBy(kv => kv.Value.id).Select(r => new Dropdown.OptionData(r.Value.id)).ToList());

            var filterMinAgeLayout = scrollLayoutLinkFiles.Grid();
            filterMinAgeLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterMinAgeLayout.Text("最低年齢"._("Minimum Age"));
            fieldMinAge = filterMinAgeLayout.InputText("");
            //fieldMinAge.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
            //fieldMinAge.inputTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);

            var filterMaxAgeLayout = scrollLayoutLinkFiles.Grid();
            filterMaxAgeLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterMaxAgeLayout.Text("最高年齢"._("Maximum Age"));
            fieldMaxAge = filterMaxAgeLayout.InputText("");
            //fieldMaxAge.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
            //fieldMaxAge.inputTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);

            #endregion
            scrollLayoutLinkFiles.Button("選択をプールに追加する"._("Add selection to pool"), () =>
            {

                var typeFilter = new List<string>() { SpecificPortraits.allTextJP, SpecificPortraits.allText }.Contains(filterDropdownType.options[filterDropdownType.value].text) ? "all" : filterDropdownType.options[filterDropdownType.value].text;
                var genderFilter = new List<string>() { SpecificPortraits.allTextJP, SpecificPortraits.allText }.Contains(filterDropdownGender.options[filterDropdownGender.value].text) ? "all" : filterDropdownGender.options[filterDropdownGender.value].text;
                var raceFilter = new List<string>() { SpecificPortraits.allTextJP, SpecificPortraits.allText }.Contains(filterDropdownRace.options[filterDropdownRace.value].text) ? "all" : filterDropdownRace.options[filterDropdownRace.value].text;
                var jobFilter = new List<string>() { SpecificPortraits.allTextJP, SpecificPortraits.allText }.Contains(filterDropdownJob.options[filterDropdownJob.value].text) ? "all" : filterDropdownJob.options[filterDropdownJob.value].text;
                var idFilter = string.IsNullOrEmpty(fieldID.Text) ? "all" : fieldID.Text;
                int.TryParse(fieldMinAge.Text, out var minAge);
                int.TryParse(fieldMaxAge.Text, out var maxAge);


                portraitSelectorView.AddPortraitsToPool(new PortraitPoolTag
                {
                    Type = filterDropdownType.options[filterDropdownType.value].text,
                    ModID = SpecificPortraits.loadedModPackages[portraitSelectorView.CurrentMod].dirInfo.Name,
                    Gender = filterDropdownGender.options[filterDropdownGender.value].text,
                    Race = raceFilter,
                    CardID = idFilter,
                    Job = jobFilter,
                    AgeMin = minAge,
                    AgeMax = maxAge
                });
                portraitSelectorView.RefreshPoolList();
            });

            poolListGrid = scrollLayoutLinkFiles.Grid();
            //poolListGrid.transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 700f);
            poolListGrid.Layout.cellSize = new Vector2(220, 40);

            scrollLayoutLinkFiles.Button("プールをリンクファイルに保存する"._("Save pools to link file"), () =>
            {
                portraitSelectorView.SavePoolFile();
            });


            ELayer.debug.enable = false;
            ELayer.player.flags.debugEnabled = false;
        }
    }
}
