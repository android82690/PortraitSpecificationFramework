using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YKF;

namespace SpecificPortraits.UI
{
    public class PSFLayerMain : YKLayer<object>
    {
        public override string Title { get; } = "ポートレート ツール"._("Portrait Tools");
        public override Rect Bound { get; } = new Rect(0.0f, 0.0f, 1400f, 800f);
        public override void OnLayout()
        {
            CreateTab<TabPicker>("ポートレートセレクター"._("Portrait Picker"), "psf.tool.picker");
            CreateTab<TabPools>("ポートレートセット"._("Portrait Pool Links"), "psf.tool.pools");
            CreateTab<TabSettings>("設定"._("Settings"), "psf.tool.settings");
        }
    }
}
