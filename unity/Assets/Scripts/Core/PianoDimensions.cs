namespace Piamo
{
    /// <summary>
    /// 鍵盤の寸法。Blender の blender/scripts/build_piano_kit.py と同じ値（1 = 白鍵 1 本ぶんのはば）。
    /// Unity の座標では X = 鍵盤のならび（右へ +）、Y = 上、Z = おく（奥へ +）。白鍵の上の面が Y = 0、前のはしが Z = 0。
    /// </summary>
    public static class PianoDimensions
    {
        public const float Pitch = 1f;
        public const float WhiteWidth = 0.92f;
        public const float WhiteLength = 4.6f;
        public const float WhiteHeight = 0.55f;
        public const float BlackWidth = 0.56f;
        public const float BlackLength = 2.8f;
        public const float BlackHeight = 0.42f;

        public const float CaseEndWidth = 0.8f;
        public const float CaseFrontZ = -0.55f;
        public const float CaseBackZ = 6.2f;
        public const float CaseBottom = -1.05f;
        public const float CaseBackTop = 1.25f;

        /// <summary>鍵盤がしずむときの、回転の中心（奥の下）。</summary>
        public const float PivotZ = 6.4f;
        public const float PivotY = -0.4f;
        /// <summary>おしたときに、前のはしがさがる角度（度）。</summary>
        public const float WhitePressAngle = 3.6f;
        public const float BlackPressAngle = 3.2f;
    }
}
