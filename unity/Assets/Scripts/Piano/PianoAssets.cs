using UnityEngine;

namespace Piamo
{
    /// <summary>鍵盤とケースの形（Blender の FBX）とマテリアル。ProjectSetup が作る。</summary>
    [CreateAssetMenu(menuName = "Piamo/Piano Assets")]
    public class PianoAssets : ScriptableObject
    {
        public Mesh whiteFull;
        public Mesh whiteLeft;
        public Mesh whiteRight;
        public Mesh whiteBoth;
        public Mesh black;
        public Mesh caseEndLeft;
        public Mesh caseEndRight;
        public Mesh caseBack;
        public Mesh caseFront;
        public Mesh caseFelt;
        public Mesh caseBed;

        public Material keyMaterial;
        public Material caseMaterial;
        public Material feltMaterial;
        public Material bedMaterial;
        public Material backdropMaterial;
        public Material logoMaterial;

        public Mesh WhiteFor(Notch n)
        {
            switch (n)
            {
                case Notch.Left: return whiteLeft;
                case Notch.Right: return whiteRight;
                case Notch.Both: return whiteBoth;
                default: return whiteFull;
            }
        }
    }
}
