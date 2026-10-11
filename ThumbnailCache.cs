using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SpecificPortraits
{
    /// <summary>
    /// Small copies of every portrait, packed into atlas pages that are saved under the
    /// BepInEx cache folder. The grids draw from the atlas, so a full-size portrait is only
    /// decoded once, when its thumbnail is first made. Later launches load the pages in a
    /// fraction of a second. Entries are keyed by file path, size and modification time,
    /// so a changed or added portrait is re-thumbnailed and a removed one is dropped.
    /// </summary>
    public static class ThumbnailCache
    {
        public const int ThumbW = 112;
        public const int ThumbH = 150;
        public const int PageSize = 2048;
        const int Cols = PageSize / ThumbW;
        const int Rows = PageSize / ThumbH;
        const int SlotsPerPage = Cols * Rows;
        const int FormatVersion = 1;
        const double FrameBudgetMs = 8.0;

        class Entry
        {
            public string Id;
            public string Path;
            public long Size;
            public long Mtime;
            public int Slot;
        }

        class Manifest
        {
            public int Version = FormatVersion;
            public int ThumbW = ThumbnailCache.ThumbW;
            public int ThumbH = ThumbnailCache.ThumbH;
            public int PageSize = ThumbnailCache.PageSize;
            public int NextSlot;
            public List<Entry> Entries = new List<Entry>();
        }

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly List<Texture2D> pages = new List<Texture2D>();
        static Manifest manifest;
        static bool running;

        public static bool Ready { get; private set; }
        public static string Dir => Path.Combine(Paths.CachePath, "PortraitSpecificationFramework");

        public static Sprite Get(string id)
        {
            return id != null && sprites.TryGetValue(id, out var s) ? s : null;
        }

        public static void Start()
        {
            if (running || SpecificPortraits.Instance == null)
                return;
            running = true;
            SpecificPortraits.Instance.StartCoroutine(Build());
        }

        static IEnumerator Build()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int reused = 0, made = 0, dropped = 0;
            try
            {
                Load();
                reused = manifest.Entries.Count;
            }
            catch (Exception ex)
            {
                SpecificPortraits.Log.LogWarning($"Thumbnail cache could not be read, rebuilding: {ex.Message}");
                Reset();
            }

            // Validate what was loaded against the portraits the game has now.
            var current = new Dictionary<string, ModItem<Sprite>>();
            foreach (var item in Portrait.modPortraits.list)
            {
                if (item.id != null && item.fileInfo != null)
                    current[item.id] = item;
            }
            var valid = new List<Entry>();
            foreach (var e in manifest.Entries)
            {
                if (current.TryGetValue(e.Id, out var item) && Matches(e, item))
                    valid.Add(e);
                else
                    dropped++;
            }
            manifest.Entries = valid;
            foreach (var e in valid)
                Register(e);

            var missing = current.Values.Where(i => !sprites.ContainsKey(i.id)).ToList();
            SpecificPortraits.Log.LogInfo($"Thumbnail cache: {valid.Count} ready, {missing.Count} to make, {dropped} stale, loaded in {sw.ElapsedMilliseconds} ms");
            Ready = true;

            if (missing.Count > 0)
            {
                var touched = new HashSet<int>();
                var frame = System.Diagnostics.Stopwatch.StartNew();
                foreach (var item in missing)
                {
                    try
                    {
                        var entry = MakeThumbnail(item);
                        if (entry != null)
                        {
                            manifest.Entries.Add(entry);
                            Register(entry);
                            touched.Add(entry.Slot / SlotsPerPage);
                            made++;
                        }
                    }
                    catch (Exception ex)
                    {
                        SpecificPortraits.Log.LogWarning($"No thumbnail for {item.id}: {ex.Message}");
                    }
                    if (frame.Elapsed.TotalMilliseconds > FrameBudgetMs)
                    {
                        yield return null;
                        frame.Restart();
                    }
                }
                foreach (int p in touched)
                {
                    pages[p].Apply(false, false);
                    yield return null;
                }
                try
                {
                    Save(touched);
                }
                catch (Exception ex)
                {
                    SpecificPortraits.Log.LogWarning($"Thumbnail cache could not be saved: {ex.Message}");
                }
            }
            SpecificPortraits.Log.LogInfo($"Thumbnail cache: {made} made, {sprites.Count} total, {pages.Count} pages, {sw.Elapsed.TotalSeconds:F1} s");
            running = false;
        }

        static bool Matches(Entry e, ModItem<Sprite> item)
        {
            var fi = item.fileInfo;
            return string.Equals(e.Path, fi.FullName, StringComparison.OrdinalIgnoreCase)
                && e.Size == fi.Length
                && e.Mtime == fi.LastWriteTimeUtc.Ticks;
        }

        static void Reset()
        {
            manifest = new Manifest();
            foreach (var p in pages)
                UnityEngine.Object.Destroy(p);
            pages.Clear();
            sprites.Clear();
        }

        static void Load()
        {
            manifest = new Manifest();
            var manifestPath = Path.Combine(Dir, "manifest.json");
            if (!File.Exists(manifestPath))
                return;
            var m = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(manifestPath));
            if (m == null || m.Version != FormatVersion || m.ThumbW != ThumbW || m.ThumbH != ThumbH || m.PageSize != PageSize)
                return;
            int pageCount = (m.NextSlot + SlotsPerPage - 1) / SlotsPerPage;
            for (int i = 0; i < pageCount; i++)
            {
                var pagePath = Path.Combine(Dir, $"page{i}.png");
                if (!File.Exists(pagePath))
                    throw new FileNotFoundException(pagePath);
                var tex = NewPage();
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(pagePath), false) || tex.width != PageSize || tex.height != PageSize)
                    throw new InvalidDataException(pagePath);
                pages.Add(tex);
            }
            manifest = m;
        }

        static void Save(HashSet<int> touchedPages)
        {
            Directory.CreateDirectory(Dir);
            foreach (int p in touchedPages)
            {
                File.WriteAllBytes(Path.Combine(Dir, $"page{p}.png"), ImageConversion.EncodeToPNG(pages[p]));
            }
            File.WriteAllText(Path.Combine(Dir, "manifest.json"), JsonConvert.SerializeObject(manifest));
        }

        static Texture2D NewPage()
        {
            var tex = new Texture2D(PageSize, PageSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "PSF thumbnail page";
            return tex;
        }

        static void SlotRect(int slot, out int page, out int x, out int y)
        {
            page = slot / SlotsPerPage;
            int local = slot % SlotsPerPage;
            x = (local % Cols) * ThumbW;
            y = (local / Cols) * ThumbH;
        }

        static void Register(Entry e)
        {
            SlotRect(e.Slot, out int page, out int x, out int y);
            while (pages.Count <= page)
                pages.Add(NewPage());
            var sprite = Sprite.Create(pages[page], new Rect(x, y, ThumbW, ThumbH), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = e.Id;
            sprites[e.Id] = sprite;
        }

        /// <summary>Decode one portrait, shrink it into the next free slot, and describe it.</summary>
        static Entry MakeThumbnail(ModItem<Sprite> item)
        {
            var fi = item.fileInfo;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(src, File.ReadAllBytes(fi.FullName), false))
                    return null;
                int slot = manifest.NextSlot++;
                SlotRect(slot, out int page, out int x, out int y);
                while (pages.Count <= page)
                    pages.Add(NewPage());
                pages[page].SetPixels32(x, y, ThumbW, ThumbH, Shrink(src.GetPixels32(), src.width, src.height));
                return new Entry { Id = item.id, Path = fi.FullName, Size = fi.Length, Mtime = fi.LastWriteTimeUtc.Ticks, Slot = slot };
            }
            finally
            {
                UnityEngine.Object.Destroy(src);
            }
        }

        /// <summary>Box-filter the image to fit the thumbnail box, centred horizontally and sitting on the bottom edge.</summary>
        static Color32[] Shrink(Color32[] src, int sw, int sh)
        {
            var dst = new Color32[ThumbW * ThumbH];
            float scale = Math.Min((float)ThumbW / sw, (float)ThumbH / sh);
            if (scale > 1f)
                scale = 1f;
            int dw = Math.Max(1, (int)(sw * scale));
            int dh = Math.Max(1, (int)(sh * scale));
            int ox = (ThumbW - dw) / 2;
            for (int dy = 0; dy < dh; dy++)
            {
                int sy0 = dy * sh / dh;
                int sy1 = Math.Max(sy0 + 1, (dy + 1) * sh / dh);
                for (int dx = 0; dx < dw; dx++)
                {
                    int sx0 = dx * sw / dw;
                    int sx1 = Math.Max(sx0 + 1, (dx + 1) * sw / dw);
                    long r = 0, g = 0, b = 0, a = 0;
                    int n = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                    {
                        int row = sy * sw;
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            var c = src[row + sx];
                            // weight colour by alpha so transparent pixels do not darken edges
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                            n++;
                        }
                    }
                    Color32 o = default;
                    if (a > 0)
                    {
                        o.r = (byte)(r / a);
                        o.g = (byte)(g / a);
                        o.b = (byte)(b / a);
                        o.a = (byte)(a / n);
                    }
                    dst[dy * ThumbW + ox + dx] = o;
                }
            }
            return dst;
        }
    }
}
