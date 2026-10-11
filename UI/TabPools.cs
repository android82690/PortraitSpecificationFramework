using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YKF;

namespace SpecificPortraits.UI
{
    /// <summary>Link portraits to pools, preview who they would apply to, and save them to a link file.</summary>
    public class TabPools : YKLayout<object>
    {
        public UIDropdown filterDropdownMod;

        UIDropdown filterDropdownType;
        UIDropdown filterDropdownGender;
        UIDropdown filterDropdownRace;
        UIDropdown filterDropdownJob;

        UIInputText fieldID;
        UIInputText fieldMinAge;
        UIInputText fieldMaxAge;
        YKGrid poolListGrid;
        UIText previewText;
        UIText statusText;
        UIText hoverText;
        ViewPortraitSelector portraitSelectorView;

        static string AllLabel => SpecificPortraits.allTextJP._(SpecificPortraits.allText);

        /// <summary>Dropdown text to a pool field: the "all" entry in any language becomes "all".</summary>
        static string FilterValue(UIDropdown dropdown)
        {
            var text = dropdown.options[dropdown.value].text;
            return text == SpecificPortraits.allText || text == SpecificPortraits.allTextJP ? "all" : text;
        }

        public void UpdatePoolList(List<string> pools)
        {
            if (poolListGrid == null)
                return;
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

        /// <summary>Shows which packages have pool edits that are not saved yet.</summary>
        public void UpdateStatus()
        {
            if (statusText == null)
                return;
            if (LinkFiles.Dirty.Count == 0)
            {
                statusText.text = "";
                return;
            }
            var names = LinkFiles.Dirty.Select(id => LinkFiles.FindPackage(id)?.title ?? id);
            statusText.text = ("未保存の変更: "._("Unsaved changes: ")) + string.Join(", ", names);
        }

        PortraitPoolTag BuildTag()
        {
            var idFilter = string.IsNullOrEmpty(fieldID.Text) ? "all" : fieldID.Text.Trim();
            int.TryParse(fieldMinAge.Text, out var minAge);
            int.TryParse(fieldMaxAge.Text, out var maxAge);
            return new PortraitPoolTag
            {
                ModID = portraitSelectorView.CurrentPackage.dirInfo.Name,
                Type = FilterValue(filterDropdownType),
                Gender = FilterValue(filterDropdownGender),
                Race = FilterValue(filterDropdownRace),
                CardID = idFilter,
                Job = FilterValue(filterDropdownJob),
                AgeMin = minAge,
                AgeMax = maxAge
            };
        }

        public override void OnLayout()
        {
            if (SpecificPortraits.loadedModPackages.Count == 0)
            {
                Text("ポートレートが見つかりません"._("No portrait packages are loaded."));
                return;
            }

            #region header
            // One row, every control with a fixed width, so nothing stretches or wraps.
            YKHorizontal header = Horizontal();
            header.Layout.spacing = 10f;
            header.Layout.childAlignment = TextAnchor.MiddleLeft;
            header.Layout.childForceExpandWidth = false;
            TabPicker.FixedWidth(header.Text("モッドフィルター"._("Mod Filter")), 110f);
            filterDropdownMod = TabPicker.FixedWidth(header.Dropdown(), 300f);
            filterDropdownMod.ClearOptions();
            filterDropdownMod.AddOptions(SpecificPortraits.loadedModPackages.Select(package => new Dropdown.OptionData(package.title)).ToList());
            TabPicker.FixedWidth(header.Button("すべて選択解除"._("Deselect All"), () =>
            {
                portraitSelectorView?.ClearSelection();
            }), 220f);
            TabPicker.FixedWidth(header.Text("ポートレート検索"._("Find portrait")), 130f);
            var search = TabPicker.FixedWidth(header.InputText(""), 320f);
            search.field.onValueChanged.AddListener(v => portraitSelectorView?.Filter(v));

            hoverText = Text(" ");
            #endregion

            YKGrid gridLayoutMain = TabPicker.PaneGrid(this, 680f);

            #region portrait picker
            portraitSelectorView = new ViewPortraitSelector(this, filterDropdownMod, gridLayoutMain);
            portraitSelectorView.OnHover = label =>
            {
                if (hoverText != null)
                    hoverText.text = label ?? "";
            };
            #endregion

            #region selection
            var scrollLayoutLinkFiles = gridLayoutMain.Vertical();

            var filterTypeLayout = scrollLayoutLinkFiles.Grid();
            filterTypeLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterTypeLayout.Text("タイプを選択する"._("Select Type"));
            filterDropdownType = filterTypeLayout.Dropdown();
            filterDropdownType.ClearOptions();
            filterDropdownType.AddOptions(new List<Dropdown.OptionData> { new Dropdown.OptionData(AllLabel), new Dropdown.OptionData("c"), new Dropdown.OptionData("guard"), new Dropdown.OptionData("foxfolk"), new Dropdown.OptionData("special") });

            var filterGenderLayout = scrollLayoutLinkFiles.Grid();
            filterGenderLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterGenderLayout.Text("性別を選択する"._("Select Gender"));
            filterDropdownGender = filterGenderLayout.Dropdown();
            filterDropdownGender.ClearOptions();
            filterDropdownGender.AddOptions(new List<Dropdown.OptionData> { new Dropdown.OptionData(AllLabel), new Dropdown.OptionData("f"), new Dropdown.OptionData("m") });

            var filterRaceLayout = scrollLayoutLinkFiles.Grid();
            filterRaceLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterRaceLayout.Text("種族を選択する"._("Select Race"));
            filterDropdownRace = filterRaceLayout.Dropdown();
            filterDropdownRace.ClearOptions();
            filterDropdownRace.AddOptions(new List<Dropdown.OptionData> { new Dropdown.OptionData(AllLabel) });
            filterDropdownRace.AddOptions(EClass.sources.races.map.OrderBy(kv => kv.Value.id).Select(r => new Dropdown.OptionData(r.Value.id)).ToList());

            var filterIDLayout = scrollLayoutLinkFiles.Grid();
            filterIDLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterIDLayout.Text("IDを入力する"._("Enter ID"));
            fieldID = filterIDLayout.InputText("");
            filterIDLayout.Button("検索"._("Search"), () =>
            {
                Dialog.InputName("ID検索"._("ID Search"), fieldID.Text == "all" ? "" : fieldID.Text, (cancel, text_input) =>
                {
                    if (cancel || string.IsNullOrEmpty(text_input))
                        return;

                    var viableIDs = EClass.sources.cards.map
                        .Where(kv => kv.Value.isChara && (kv.Value.id.Contains(text_input) || kv.Value.name.Contains(text_input) || kv.Value.name_JP.Contains(text_input)))
                        .Select(i => new { id = i.Value.id, name = (Lang.isJP ? i.Value.name_JP : i.Value.name) })
                        .Take(10)
                        .ToList();

                    Dialog.Choice("ID検索"._("ID Search"), (d) =>
                    {
                        d.AddButton(AllLabel, () => { fieldID.Text = "all"; });
                        foreach (var choice in viableIDs)
                        {
                            d.AddButton($"{choice.id}, {choice.name}", () => { fieldID.Text = choice.id; });
                        }
                    });
                });
            });

            var filterJobLayout = scrollLayoutLinkFiles.Grid();
            filterJobLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterJobLayout.Text("職業を選択する"._("Select Job"));
            filterDropdownJob = filterJobLayout.Dropdown();
            filterDropdownJob.ClearOptions();
            filterDropdownJob.AddOptions(new List<Dropdown.OptionData> { new Dropdown.OptionData(AllLabel) });
            filterDropdownJob.AddOptions(EClass.sources.jobs.map.OrderBy(kv => kv.Value.id).Select(r => new Dropdown.OptionData(r.Value.id)).ToList());

            var filterMinAgeLayout = scrollLayoutLinkFiles.Grid();
            filterMinAgeLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterMinAgeLayout.Text("最低年齢"._("Minimum Age"));
            fieldMinAge = filterMinAgeLayout.InputText("");

            var filterMaxAgeLayout = scrollLayoutLinkFiles.Grid();
            filterMaxAgeLayout.Layout.cellSize = new Vector2(200f, 50f);
            filterMaxAgeLayout.Text("最高年齢"._("Maximum Age"));
            fieldMaxAge = filterMaxAgeLayout.InputText("");
            #endregion

            var actionGrid = scrollLayoutLinkFiles.Grid();
            actionGrid.Layout.cellSize = new Vector2(320f, 40f);
            actionGrid.Layout.constraintCount = 2;
            actionGrid.Button("選択をプールに追加する"._("Add selection to pool"), () =>
            {
                portraitSelectorView.AddPortraitsToPool(BuildTag());
                portraitSelectorView.RefreshPoolList();
            });
            actionGrid.Button("このマップで一致を確認"._("Preview matches on this map"), () =>
            {
                var matches = PortraitPatch.PreviewMatches(BuildTag());
                if (previewText == null)
                    return;
                if (SpecificPortraits.CurrentMap == null)
                {
                    previewText.text = "ゲームをロードしてください"._("Load a game to preview.");
                    return;
                }
                var names = matches.Select(c => c.Name).Distinct().Take(40).ToList();
                var head = ("一致するキャラクター"._("Matching characters")) + $" ({matches.Count})";
                previewText.text = matches.Count == 0
                    ? head + ": " + ("なし"._("none"))
                    : head + ": " + string.Join(", ", names) + (matches.Count > names.Count ? " ..." : "");
            });

            previewText = scrollLayoutLinkFiles.Text("");
            previewText.horizontalOverflow = HorizontalWrapMode.Wrap;
            previewText.verticalOverflow = VerticalWrapMode.Overflow;
            var previewLe = previewText.gameObject.AddComponent<LayoutElement>();
            previewLe.minWidth = 200f;
            previewLe.flexibleWidth = 1f;
            previewText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            poolListGrid = scrollLayoutLinkFiles.Grid();
            poolListGrid.Layout.cellSize = new Vector2(220, 40);

            scrollLayoutLinkFiles.Button("プールをリンクファイルに保存する"._("Save pools to link file"), () =>
            {
                portraitSelectorView.SavePoolFile();
            });
            statusText = scrollLayoutLinkFiles.Text("");
            UpdateStatus();
        }
    }
}
