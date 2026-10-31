using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    /// <summary>せっていなどで使う、小さな子でも さわりやすい部品。</summary>
    public static class UIParts
    {
        public static VisualElement El(string cls = null, VisualElement parent = null, bool pick = false)
        {
            var e = new VisualElement();
            if (!string.IsNullOrEmpty(cls)) foreach (var c in cls.Split(' ')) e.AddToClassList(c);
            e.pickingMode = pick ? PickingMode.Position : PickingMode.Ignore;
            parent?.Add(e);
            return e;
        }

        public static Label Text(string text, string cls = null, VisualElement parent = null)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            if (!string.IsNullOrEmpty(cls)) foreach (var c in cls.Split(' ')) l.AddToClassList(c);
            parent?.Add(l);
            return l;
        }

        /// <summary>アイコンと文字のボタン（あめ玉のボタン）。</summary>
        public static Button Candy(string text, string color, Texture2D icon, Action onClick, VisualElement parent = null, string extra = null)
        {
            var b = new Button { text = "" };
            b.AddToClassList("candy");
            if (!string.IsNullOrEmpty(color)) b.AddToClassList("candy--" + color);
            if (!string.IsNullOrEmpty(extra)) foreach (var c in extra.Split(' ')) b.AddToClassList(c);
            if (icon != null)
            {
                var i = El("candy__icon", b);
                i.style.backgroundImage = new StyleBackground(icon);
            }
            if (!string.IsNullOrEmpty(text)) Text(text, "candy__label", b);
            if (onClick != null) b.clicked += onClick;
            parent?.Add(b);
            return b;
        }

        public static void SetIcon(VisualElement e, Texture2D tex)
        {
            if (e == null) return;
            e.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
        }
    }

    /// <summary>いくつかの中から 1 つえらぶ（ならんだボタン）。</summary>
    public class Segmented : VisualElement
    {
        readonly List<Button> _items = new List<Button>();
        public int Value { get; private set; }
        public event Action<int> Changed;

        public Segmented(IList<string> labels, int value)
        {
            AddToClassList("seg");
            for (int i = 0; i < labels.Count; i++)
            {
                int k = i;
                var b = new Button(() => Select(k, true)) { text = labels[i] };
                b.AddToClassList("seg__item");
                _items.Add(b);
                Add(b);
            }
            Select(value, false);
        }

        public Button Item(int i) => _items[i];

        public void Select(int i, bool notify)
        {
            Value = Mathf.Clamp(i, 0, _items.Count - 1);
            for (int k = 0; k < _items.Count; k++) _items[k].EnableInClassList("seg__item--on", k == Value);
            if (notify) Changed?.Invoke(Value);
        }
    }

    /// <summary>オン・オフの スイッチ。</summary>
    public class Switch : Button
    {
        readonly Label _text;
        readonly string _on, _off;
        public bool Value { get; private set; }
        public event Action<bool> Changed;

        public Switch(bool value, string onText = "オン", string offText = "オフ")
        {
            _on = onText;
            _off = offText;
            AddToClassList("switch");
            UIParts.El("switch__knob", this);
            _text = new Label { pickingMode = PickingMode.Ignore };
            _text.AddToClassList("switch__text");
            clicked += () => Set(!Value, true);
            Set(value, false);
        }

        /// <summary>スイッチと、となりの「オン／オフ」の字をならべたもの。</summary>
        public VisualElement WithText()
        {
            var row = UIParts.El("stepper");
            row.Add(this);
            row.Add(_text);
            return row;
        }

        public void Set(bool v, bool notify)
        {
            Value = v;
            EnableInClassList("switch--on", v);
            _text.text = v ? _on : _off;
            if (notify) Changed?.Invoke(v);
        }
    }

    /// <summary>ゆびで うごかす スライダー（0〜1）。</summary>
    public class SoftSlider : VisualElement
    {
        readonly VisualElement _track, _fill, _knob;
        int _pointer = -1;
        public float Value { get; private set; }
        public event Action<float> Changed;
        public event Action Released;

        public SoftSlider(float value)
        {
            AddToClassList("slider");
            pickingMode = PickingMode.Position;
            _track = UIParts.El("slider__track", this);
            _fill = UIParts.El("slider__fill", _track);
            _knob = UIParts.El("slider__knob", this);
            RegisterCallback<PointerDownEvent>(e =>
            {
                _pointer = e.pointerId;
                this.CapturePointer(e.pointerId);
                SetFromX(e.localPosition.x);
                e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.pointerId != _pointer) return;
                SetFromX(e.localPosition.x);
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != _pointer) return;
                this.ReleasePointer(e.pointerId);
                _pointer = -1;
                Released?.Invoke();
            });
            RegisterCallback<GeometryChangedEvent>(_ => Redraw());
            Set(value, false);
        }

        void SetFromX(float x)
        {
            float w = Mathf.Max(1f, layout.width - 70f);
            Set(Mathf.Clamp01((x - 35f) / w), true);
        }

        public void Set(float v, bool notify)
        {
            v = Mathf.Clamp01(v);
            bool changed = !Mathf.Approximately(v, Value);
            Value = v;
            Redraw();
            if (notify && changed) Changed?.Invoke(v);
        }

        void Redraw()
        {
            _fill.style.width = Length.Percent(Value * 100f);
            float w = Mathf.Max(1f, layout.width - 70f);
            _knob.style.left = Value * w;
        }
    }

    /// <summary>「−  あたい  ＋」</summary>
    public class Stepper : VisualElement
    {
        readonly Label _value;
        readonly Button _minus, _plus;
        public Stepper(Texture2D minusIcon, Texture2D plusIcon, Action minus, Action plus)
        {
            AddToClassList("stepper");
            _minus = UIParts.Candy(null, "white", minusIcon, minus, this, "round-button");
            _value = UIParts.Text("", "stepper__value", this);
            _plus = UIParts.Candy(null, "white", plusIcon, plus, this, "round-button");
        }

        public void Set(string text, bool canMinus, bool canPlus)
        {
            _value.text = text;
            _minus.SetEnabled(canMinus);
            _plus.SetEnabled(canPlus);
            _minus.style.opacity = canMinus ? 1f : 0.35f;
            _plus.style.opacity = canPlus ? 1f : 0.35f;
        }
    }

    /// <summary>長おしで はじめて つかえるボタン（おうちの かた むけ。まちがって けさないように）。</summary>
    public class HoldButton : Button
    {
        readonly VisualElement _fill;
        float _held = -1f;
        readonly float _seconds;
        public event Action Completed;

        public HoldButton(string text, string color, float seconds)
        {
            _seconds = seconds;
            AddToClassList("candy");
            AddToClassList("candy--" + color);
            AddToClassList("hold-button");
            _fill = UIParts.El("hold-fill", this);
            UIParts.Text(text, "candy__label", this);
            RegisterCallback<PointerDownEvent>(_ => _held = 0f, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(_ => Cancel(), TrickleDown.TrickleDown);
            RegisterCallback<PointerLeaveEvent>(_ => Cancel());
            schedule.Execute(Tick).Every(33);
        }

        void Cancel()
        {
            _held = -1f;
            _fill.style.width = 0;
        }

        void Tick(TimerState ts)
        {
            if (_held < 0f) return;
            _held += ts.deltaTime / 1000f;
            _fill.style.width = Length.Percent(Mathf.Clamp01(_held / _seconds) * 100f);
            if (_held >= _seconds)
            {
                Cancel();
                Completed?.Invoke();
            }
        }

        /// <summary>テスト用：長おしが おわったことにする。</summary>
        public void Complete() => Completed?.Invoke();
    }
}
