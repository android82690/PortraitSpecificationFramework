using YKF;
using UnityEngine;

namespace SpecificPortraits.UI
{
    public class TabSettings : YKLayout<object>
    {
        public override void OnLayout()
        {
            YKVertical scrollLayout = Vertical();
            scrollLayout.Layout.spacing = 10f;

            YKGrid gridLayout = scrollLayout.Grid();
            gridLayout.Layout.cellSize = new Vector2(400f, 40f);
            gridLayout.Rect().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50f);
            gridLayout.Toggle("「ポートレートの再ロール」ダイアログオプションを有効にする"._("Enable \"Reroll Portrait\" dialog option"), SpecificPortraits.EnableRerollDialog.Value, isOn =>
            {
                SpecificPortraits.EnableRerollDialog.Value = isOn;

            });

            YKGrid forceVanillaLayout = scrollLayout.Grid();
            //forceVanillaLayout.Rect().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50f);
            forceVanillaLayout.Layout.cellSize = new Vector2(400f, 40f);
            forceVanillaLayout.Toggle("常にバニラポートレートを含める"._("Always include vanilla portraits"), SpecificPortraits.AlwaysIncludeVanillaPortraits.Value, isOn =>
            {
                SpecificPortraits.AlwaysIncludeVanillaPortraits.Value = isOn;

            });
        }
    }
}
