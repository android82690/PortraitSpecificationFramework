using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpecificPortraits.UI
{
    /// <summary>
    /// Fills portrait images lazily. Inside a scroll view only the cells near the
    /// viewport are decoded, a few per frame, so a grid of a thousand portraits opens
    /// at once and loads as it is scrolled. Outside a scroll view it falls back to a
    /// plain few-per-frame coroutine.
    /// </summary>
    public static class SpriteBatchLoader
    {
        /// <summary>Time spent decoding per frame. Cached sprites cost almost nothing, so many of those fit in one frame.</summary>
        public const double FrameBudgetMs = 6.0;

        public static void Start(MonoBehaviour host, List<KeyValuePair<Image, string>> items)
        {
            if (host == null || items.Count == 0)
                return;

            var scroll = items[0].Key.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                var view = scroll.viewport;
                if (view == null && scroll.content != null)
                    view = scroll.content.parent as RectTransform;
                if (view == null)
                    view = scroll.transform as RectTransform;
                var loader = scroll.gameObject.AddComponent<VisibleSpriteLoader>();
                loader.Init(items, view);
                return;
            }
            // No scroll view: load everything, a few per frame. The host may be an inactive tab,
            // whose coroutines would never run, so use the plugin object instead.
            var runner = SpecificPortraits.Instance != null ? (MonoBehaviour)SpecificPortraits.Instance : host;
            runner.StartCoroutine(RunAll(items));
        }

        public static void Apply(Image image, string id)
        {
            if (image == null)
                return;
            // The atlas thumbnail is free to show; the full portrait is only decoded when no thumbnail exists yet.
            image.sprite = ThumbnailCache.Get(id) ?? SpecificPortraits.GetSprite(id);
            image.enabled = image.sprite != null;
        }

        static IEnumerator RunAll(List<KeyValuePair<Image, string>> items)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            foreach (var kv in items)
            {
                if (kv.Key == null)
                    yield break;
                Apply(kv.Key, kv.Value);
                if (sw.Elapsed.TotalMilliseconds > FrameBudgetMs)
                {
                    yield return null;
                    sw.Restart();
                }
            }
        }
    }

    /// <summary>Decodes the sprites of cells that are within the viewport, a few per frame.</summary>
    public class VisibleSpriteLoader : MonoBehaviour
    {
        /// <summary>Extra height above and below the viewport that is loaded ahead of scrolling.</summary>
        public float Margin = 250f;

        List<KeyValuePair<Image, string>> items;
        bool[] done;
        RectTransform viewport;
        int remaining;
        readonly Vector3[] corners = new Vector3[4];

        public void Init(List<KeyValuePair<Image, string>> list, RectTransform view)
        {
            items = list;
            done = new bool[list.Count];
            viewport = view;
            remaining = list.Count;
        }

        void Update()
        {
            if (items == null || remaining == 0 || viewport == null)
                return;

            viewport.GetWorldCorners(corners);
            float minY = corners[0].y - Margin;
            float maxY = corners[1].y + Margin;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < items.Count && sw.Elapsed.TotalMilliseconds < SpriteBatchLoader.FrameBudgetMs; i++)
            {
                if (done[i])
                    continue;
                var image = items[i].Key;
                if (image == null)
                {
                    done[i] = true;
                    remaining--;
                    continue;
                }
                if (!image.gameObject.activeInHierarchy)
                    continue;

                float y = image.rectTransform.position.y;
                if (y < minY || y > maxY)
                    continue;

                SpriteBatchLoader.Apply(image, items[i].Value);
                done[i] = true;
                remaining--;
            }
        }
    }
}
