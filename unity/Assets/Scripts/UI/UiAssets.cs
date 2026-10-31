using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>UI の絵とフォント。ProjectSetup が Assets/UI から作る。</summary>
    [CreateAssetMenu(menuName = "Piamo/UI Assets")]
    public class UiAssets : ScriptableObject
    {
        [Serializable]
        public class NamedTexture
        {
            public string name;
            public Texture2D texture;
        }

        public Font bodyFont;
        public Font titleFont;
        public Texture2D logo;
        /// <summary>アイコン（Assets/UI/Icons）。楽器は inst_&lt;id&gt;。</summary>
        public List<NamedTexture> icons = new List<NamedTexture>();
        /// <summary>どうぶつの絵（Assets/UI/Characters）。</summary>
        public List<NamedTexture> characters = new List<NamedTexture>();

        Dictionary<string, Texture2D> _icons, _chars;

        public Texture2D Icon(string name) => Lookup(ref _icons, icons, name);
        public Texture2D Character(string name) => Lookup(ref _chars, characters, name);

        static Texture2D Lookup(ref Dictionary<string, Texture2D> cache, List<NamedTexture> list, string name)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, Texture2D>();
                foreach (var t in list) if (t != null && t.texture != null) cache[t.name] = t.texture;
            }
            return name != null && cache.TryGetValue(name, out var tex) ? tex : null;
        }
    }
}
