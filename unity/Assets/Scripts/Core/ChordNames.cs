using System.Collections.Generic;

namespace Piamo
{
    /// <summary>
    /// おさえている 音が 和音なら、その なまえ（ドミソ など）と ひびき（あかるい・さみしい）を こたえる。
    /// じゆうに ひく で「ドミソ！ あかるい ひびき」と ほめるため。
    /// </summary>
    public static class ChordNames
    {
        public static bool TryName(IList<int> held, LabelMode mode, out string notes, out string feel)
        {
            notes = null;
            feel = null;
            if (held == null || held.Count < 3) return false;
            var pcs = new SortedSet<int>();
            foreach (int m in held) pcs.Add(NoteUtil.PitchClass(m));
            if (pcs.Count < 3 || pcs.Count > 4) return false;
            foreach (int r in pcs)
            {
                if (Is(pcs, r, 4, 7)) feel = "あかるい ひびき";
                else if (Is(pcs, r, 3, 7)) feel = "ちょっと さみしい ひびき";
                else if (Is(pcs, r, 4, 7, 10)) feel = "おしゃれな ひびき";
                if (feel != null) break;
            }
            if (feel == null) return false;
            // 音の名前は、おさえている 音を ひくい順に（同じ音の なまえは 1 回）
            var shown = mode == LabelMode.Hiragana || mode == LabelMode.Letters ? mode : LabelMode.Katakana;
            var sorted = new List<int>(held);
            sorted.Sort();
            var seen = new HashSet<int>();
            var sb = new System.Text.StringBuilder();
            foreach (int m in sorted)
            {
                if (!seen.Add(NoteUtil.PitchClass(m))) continue;
                sb.Append(NoteUtil.Name(m, shown));
            }
            notes = sb.ToString();
            return true;
        }

        static bool Is(SortedSet<int> pcs, int root, params int[] steps)
        {
            if (pcs.Count != steps.Length + 1) return false;
            foreach (int s in steps) if (!pcs.Contains((root + s) % 12)) return false;
            return true;
        }
    }
}
