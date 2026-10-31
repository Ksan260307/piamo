using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>
    /// 3D の鍵盤。Blender で作った白鍵・黒鍵・ケースをならべ、おしたときに しずませて ひからせる。
    ///  - 白鍵は、となりに黒鍵があるところだけ切りかいた形を使う（黒鍵をかくすと、切りかきのない形）
    ///  - よこに のばすとき（Stretch）は、鍵盤とケースの板だけを のばし、左右のそでは そのまま
    ///  - まねっこのヒントは、金色に ゆっくり ひかる
    /// </summary>
    public class KeyboardView : MonoBehaviour
    {
        public PianoAssets assets;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        static readonly Color WhiteKey = new Color(1f, 0.99f, 0.96f);
        static readonly Color BlackKey = new Color(0.17f, 0.16f, 0.24f);
        static readonly Color HintColor = new Color(1f, 0.82f, 0.25f);

        class KeyObj
        {
            public KeyInfo info;
            public Transform pivot;
            public Renderer renderer;
            public bool pressed;
            public float press;
            public float glow;
            public Color glowColor;
            public Color baseColor;
            public bool hint;
            public bool dirty = true;
        }

        readonly Dictionary<int, KeyObj> _keys = new Dictionary<int, KeyObj>();
        readonly List<KeyObj> _list = new List<KeyObj>();
        MaterialPropertyBlock _mpb;
        Transform _keysRoot, _endL, _endR;
        Renderer[] _caseRenderers = new Renderer[0];
        KeyboardLayout _layout;
        int _hintKey = -1;
        float _stretch = 1f;
        Color _caseColor = PianoColors.Colors[0];

        public KeyboardLayout Layout => _layout;
        public float Stretch => _stretch;
        public Transform KeysRoot => _keysRoot;
        public int HintKey => _hintKey;

        public void Build(KeyboardLayout layout, PiamoSettings s)
        {
            _mpb ??= new MaterialPropertyBlock();
            Clear();
            _layout = layout;
            _keysRoot = new GameObject("Keys").transform;
            _keysRoot.SetParent(transform, false);

            int n = layout.WhiteCount;
            foreach (var k in layout.Keys)
            {
                var pivot = new GameObject((k.black ? "Black_" : "White_") + k.midi).transform;
                pivot.SetParent(_keysRoot, false);
                pivot.localPosition = new Vector3(k.x, PianoDimensions.PivotY, PianoDimensions.PivotZ);
                var meshGo = new GameObject("Mesh");
                meshGo.transform.SetParent(pivot, false);
                meshGo.transform.localPosition = new Vector3(0, -PianoDimensions.PivotY, -PianoDimensions.PivotZ);
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.sharedMesh = k.black ? assets.black : assets.WhiteFor(k.notch);
                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = assets.keyMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var ko = new KeyObj { info = k, pivot = pivot, renderer = mr };
                ko.baseColor = BaseColorFor(k, s);
                _keys[k.midi] = ko;
                _list.Add(ko);
            }

            var renderers = new List<Renderer>();
            renderers.Add(Segment("Case_Back", assets.caseBack, assets.caseMaterial, n));
            renderers.Add(Segment("Case_Front", assets.caseFront, assets.caseMaterial, n));
            Segment("Case_Felt", assets.caseFelt, assets.feltMaterial, n);
            Segment("Case_Bed", assets.caseBed, assets.bedMaterial, n);
            _endL = Part("Case_End_L", assets.caseEndLeft, assets.caseMaterial, transform, out var rl);
            _endR = Part("Case_End_R", assets.caseEndRight, assets.caseMaterial, transform, out var rr);
            renderers.Add(rl);
            renderers.Add(rr);
            _caseRenderers = renderers.ToArray();
            SetCaseColor(PianoColors.Colors[Mathf.Clamp(s.pianoColor, 0, PianoColors.Count - 1)]);
            SetStretch(_stretch);
        }

        Renderer Segment(string name, Mesh mesh, Material mat, int whiteCount)
        {
            var t = Part(name, mesh, mat, _keysRoot, out var r);
            t.localPosition = new Vector3(-0.5f * PianoDimensions.Pitch, 0, 0);
            t.localScale = new Vector3(whiteCount * PianoDimensions.Pitch, 1, 1);
            return r;
        }

        static Transform Part(string name, Mesh mesh, Material mat, Transform parent, out Renderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            r = mr;
            return go.transform;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            _keys.Clear();
            _list.Clear();
            _hintKey = -1;
            _keysRoot = _endL = _endR = null;
        }

        static Color BaseColorFor(KeyInfo k, PiamoSettings s)
        {
            if (k.black) return BlackKey;
            if (s != null && s.rainbowKeys) return Color.Lerp(WhiteKey, NoteUtil.NoteColor(k.midi), 0.42f);
            return WhiteKey;
        }

        /// <summary>白鍵のいろ（にじいろ など）を、作りなおさずに かえる。</summary>
        public void RefreshColors(PiamoSettings s)
        {
            foreach (var k in _list)
            {
                k.baseColor = BaseColorFor(k.info, s);
                k.dirty = true;
            }
            SetCaseColor(PianoColors.Colors[Mathf.Clamp(s.pianoColor, 0, PianoColors.Count - 1)]);
        }

        public void SetCaseColor(Color c)
        {
            _caseColor = c;
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in _caseRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        public Color CaseColor => _caseColor;

        /// <summary>鍵盤を よこに のばす（1 以上）。</summary>
        public void SetStretch(float s)
        {
            _stretch = Mathf.Max(0.1f, s);
            if (_keysRoot == null || _layout == null) return;
            _keysRoot.localScale = new Vector3(_stretch, 1, 1);
            float left = -0.5f * PianoDimensions.Pitch * _stretch;
            float right = (_layout.WhiteCount - 0.5f) * PianoDimensions.Pitch * _stretch;
            if (_endL != null) _endL.localPosition = new Vector3(left, 0, 0);
            if (_endR != null) _endR.localPosition = new Vector3(right, 0, 0);
        }

        public void SetPressed(int midi, bool down, Color? glow = null)
        {
            if (!_keys.TryGetValue(midi, out var k)) return;
            k.pressed = down;
            if (down)
            {
                k.glow = 1f;
                k.glowColor = glow ?? NoteUtil.NoteColor(midi);
            }
            k.dirty = true;
        }

        public bool IsPressed(int midi) => _keys.TryGetValue(midi, out var k) && k.pressed;

        /// <summary>おしこみの量（0〜1）。テスト用。</summary>
        public float PressAmount(int midi) => _keys.TryGetValue(midi, out var k) ? k.press : 0f;

        public void SetHint(int midi)
        {
            if (_hintKey == midi) return;
            if (_keys.TryGetValue(_hintKey, out var old))
            {
                old.hint = false;
                old.dirty = true;
            }
            _hintKey = midi;
            if (_keys.TryGetValue(midi, out var k))
            {
                k.hint = true;
                k.dirty = true;
            }
        }

        /// <summary>鍵盤の上の面の、ラベルを書く場所（ワールド座標）。白鍵は手前、黒鍵はまんなかへん。</summary>
        public Vector3 LabelPoint(int midi)
        {
            if (!_keys.TryGetValue(midi, out var k) || _keysRoot == null) return Vector3.zero;
            float z = k.info.black ? PianoDimensions.WhiteLength - PianoDimensions.BlackLength * 0.45f : 0.95f;
            float y = k.info.black ? PianoDimensions.BlackHeight : 0f;
            Vector3 local = new Vector3(k.info.x, y, z);
            // しずんでいる分も入れる
            Vector3 p = k.pivot.localPosition;
            Vector3 rel = local - p;
            rel = k.pivot.localRotation * rel;
            return _keysRoot.TransformPoint(p + rel);
        }

        /// <summary>鍵盤の奥の上（おんぷが とびだす場所）。</summary>
        public Vector3 TopPoint(int midi)
        {
            if (!_keys.TryGetValue(midi, out var k) || _keysRoot == null) return Vector3.zero;
            return _keysRoot.TransformPoint(new Vector3(k.info.x, k.info.black ? PianoDimensions.BlackHeight : 0f, PianoDimensions.WhiteLength * 0.8f));
        }

        void Update()
        {
            if (_list.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            foreach (var k in _list)
            {
                float target = k.pressed ? 1f : 0f;
                float speed = k.pressed ? 28f : 11f;
                float before = k.press;
                k.press = Mathf.MoveTowards(k.press, target, dt * speed);
                bool moved = !Mathf.Approximately(before, k.press);
                if (moved)
                {
                    float ang = k.info.black ? PianoDimensions.BlackPressAngle : PianoDimensions.WhitePressAngle;
                    float e = k.press * k.press * (3f - 2f * k.press);
                    k.pivot.localRotation = Quaternion.Euler(-e * ang, 0, 0);
                }
                if (!k.pressed && k.glow > 0f)
                {
                    k.glow = Mathf.Max(0f, k.glow - dt / 0.45f);
                    k.dirty = true;
                }
                if (k.hint) k.dirty = true;
                if (!k.dirty && !moved) continue;
                k.dirty = false;
                Color baseCol = k.baseColor;
                Color emit = Color.black;
                if (k.glow > 0f)
                {
                    float g = k.glow;
                    baseCol = Color.Lerp(baseCol, k.glowColor, (k.info.black ? 0.55f : 0.45f) * g);
                    emit += k.glowColor * (0.35f * g);
                }
                if (k.hint)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6.2f);
                    baseCol = Color.Lerp(baseCol, HintColor, 0.35f + 0.3f * pulse);
                    emit += HintColor * (0.25f + 0.45f * pulse);
                }
                k.renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, baseCol);
                _mpb.SetColor(EmissionId, emit);
                k.renderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
