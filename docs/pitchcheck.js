// Piamo: 音の高さの けんさ。index.html?pitchtest で ひらいたときだけ、Unity より先に 読みこむ（TESTING.md「WebGL の音の高さ」）
//  - アプリ（AppUI.PitchSweep）は、すべての楽器の すべての音（C2〜C7）を じゅんに鳴らし、楽器ごとに「[PitchTest] <楽器> 36-96」と ログを出す
//  - ここでは、ブラウザが ほんとうに鳴らした AudioBuffer の中の音の高さ × playbackRate を はかって、ねらった高さ（A4 = 440Hz）と くらべる
//  - おわったら 結果を 画面と コンソールに出す（開発者ツールのない スマホ・iPad でも 見られる）
//  - けんさの あいだは 画面を おおって、タップで 楽器が かわったり 効果音が まざったり しないようにする
(function () {
  const TOLERANCE = 5;   // セント
  const LOOPS = new Set(["organ", "flute"]), VOICES = new Set(["cat", "dog"]);
  const NAMES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
  const plays = [], marks = [];
  let ctx = null, current = null, done = false;
  window.__piamoPitch = { plays, marks, result: null };

  // ---- 画面 ----
  const cover = document.createElement("div");
  cover.style.cssText = "position:fixed;inset:0;z-index:5;display:flex;justify-content:center;align-items:flex-start;" +
    "padding:12px;box-sizing:border-box;background:rgba(255,247,236,.25)";
  const card = document.createElement("div");
  card.style.cssText = "background:#fff;color:#5a3b2e;border-radius:16px;padding:12px 16px;box-shadow:0 6px 20px rgba(0,0,0,.15);" +
    "max-width:100%;max-height:100%;overflow:auto;box-sizing:border-box;-webkit-user-select:text;user-select:text;touch-action:pan-y";
  const title = document.createElement("b");
  title.textContent = "おとの たかさの けんさ";
  const text = document.createElement("pre");
  text.style.cssText = "margin:8px 0 0;white-space:pre-wrap;font:12px/1.5 ui-monospace,Menlo,Consolas,monospace";
  text.textContent = "よみこみ中…";
  card.append(title, text);
  cover.appendChild(card);
  document.body.appendChild(cover);
  // スマホでは さわるまで 音が でないので、さわったら 音を出す（アプリには つたえない）
  cover.addEventListener("pointerdown", () => { if (ctx && ctx.state !== "running") ctx.resume(); });

  // ---- 鳴らした音を 記録する ----
  const start = AudioBufferSourceNode.prototype.start;
  AudioBufferSourceNode.prototype.start = function (...args) {
    plays.push({ buffer: this.buffer, node: this, t: performance.now() });
    if (current && this.buffer) current.add(this.buffer);
    show();
    return start.apply(this, args);
  };
  const stop = AudioBufferSourceNode.prototype.stop;
  AudioBufferSourceNode.prototype.stop = function (...args) {
    if (this.__stoppedAt === undefined) this.__stoppedAt = performance.now();
    return stop.apply(this, args);
  };
  const Base = window.BaseAudioContext || window.AudioContext || window.webkitAudioContext;
  if (Base && Base.prototype.decodeAudioData) {
    const decode = Base.prototype.decodeAudioData;
    Base.prototype.decodeAudioData = function (...args) {
      ctx = this;
      return decode.apply(this, args);
    };
  }
  const log = console.log;
  console.log = function (...args) {
    const s = String(args[0]);
    const m = s.match(/\[PitchTest\] (\w+) (\d+)-(\d+)/);
    if (m) {
      marks.push({ name: m[1], low: +m[2], high: +m[3], at: plays.length });
      current = new Set();
      show();
    } else if (s.indexOf("[PitchTest] done") >= 0 && !done) {
      done = true;
      text.textContent = "けいさん中…";
      setTimeout(finish, 50);
    }
    return log.apply(this, args);
  };

  function show() {
    if (done) return;
    const mk = marks[marks.length - 1];
    let s = mk ? `しらべています… ${mk.name} ${current.size}/${mk.high - mk.low + 1}` : "よみこみ中…";
    if (ctx) s = `AudioContext ${ctx.sampleRate} Hz（${ctx.state === "running" ? "おとが でています" : "さわると おとが でます"}）\n` + s;
    text.textContent = s;
  }

  function finish() {
    const r = report(TOLERANCE);
    window.__piamoPitch.result = r;
    title.textContent = "おとの たかさの けんさ：" + (r.ok ? "OK" : "NG");
    title.style.color = r.ok ? "#2e8b57" : "#d0402b";
    text.textContent = r.text;
    const close = document.createElement("button");
    close.textContent = "とじる";
    close.style.cssText = "margin-top:10px;padding:6px 18px;border-radius:14px;border:2px solid #e8a0b8;background:#ffd6e2;" +
      "color:#5a3b2e;font-weight:700;font-size:14px;cursor:pointer";
    close.onclick = () => cover.remove();
    card.appendChild(close);
    log.call(console, "[PitchTest] " + (r.ok ? "OK" : "NG") + "\n" + r.text);
  }

  // ---- はかる ----
  const freq = (n) => 440 * Math.pow(2, (n - 69) / 12);
  const noteName = (n) => NAMES[n % 12] + (Math.floor(n / 12) - 1);
  // ゲルツェル法：周波数 f の 強さ
  function power(x, sr, f) {
    const k = 2 * Math.cos(2 * Math.PI * f / sr);
    let s1 = 0, s2 = 0;
    for (let i = 0; i < x.length; i++) { const s0 = x[i] + k * s1 - s2; s2 = s1; s1 = s0; }
    return s1 * s1 + s2 * s2 - k * s1 * s2;
  }
  // はかる ところ：どうぶつの声は のばしている ところ、ループは ぜんぶ、たたく音は 鳴りはじめの すぐあと
  function seg(buf, kind) {
    const sr = buf.sampleRate, d = buf.getChannelData(0), n = d.length;
    let a, len;
    if (kind === "voice") { a = Math.floor(n * 0.15); len = Math.floor(n * 0.5); }
    else if (kind === "loop") { a = 0; len = n; }
    else { a = Math.floor(0.03 * sr); len = Math.min(n - a, Math.floor(0.4 * sr)); }
    const x = new Float32Array(len);
    for (let i = 0; i < len; i++) x[i] = d[a + i] * (0.5 - 0.5 * Math.cos(2 * Math.PI * i / (len - 1)));
    return x;
  }
  // ねらった高さ ±100 セントで いちばん強いところ（2 セント → 0.1 セント）
  function cents(x, sr, expect) {
    let best = -1, bc = 0;
    for (let c = -100; c <= 100; c += 2) { const p = power(x, sr, expect * Math.pow(2, c / 1200)); if (p > best) { best = p; bc = c; } }
    const c0 = bc;
    for (let c = c0 - 2; c <= c0 + 2; c += 0.1) { const p = power(x, sr, expect * Math.pow(2, c / 1200)); if (p > best) { best = p; bc = c; } }
    return bc;
  }
  // どの音か：基音と 2 倍音の強さの合計が いちばん大きい音
  function identify(x, sr, low, high) {
    let best = -1, bn = low;
    for (let n = low; n <= high; n++) {
      const p = power(x, sr, freq(n)) + 0.5 * power(x, sr, 2 * freq(n));
      if (p > best) { best = p; bn = n; }
    }
    return bn;
  }

  function report(tolerance) {
    if (!marks.length) return { ok: false, text: "「[PitchTest] …」の ログが ありません（index.html?pitchtest で ひらきましたか？）" };
    const lines = [];
    if (ctx) lines.push(`AudioContext ${ctx.sampleRate} Hz`);
    let ok = true;
    marks.forEach((mk, i) => {
      const end = i + 1 < marks.length ? marks[i + 1].at : plays.length;
      const kind = VOICES.has(mk.name) ? "voice" : LOOPS.has(mk.name) ? "loop" : "hit";
      const expected = mk.high - mk.low + 1;
      // 鳴った じゅんに、バッファごとに まとめる（1 つの音で ノードが 2 つ できることがある）
      const order = [], nodesOf = new Map();
      for (const p of plays.slice(mk.at, end)) {
        if (!p.buffer) continue;
        if (!nodesOf.has(p.buffer)) { nodesOf.set(p.buffer, []); order.push(p.buffer); }
        nodesOf.get(p.buffer).push(p);
      }
      // ぜんぶ そろっていれば じゅんばんどおり、ちがえば 音の高さから どの音かを みつける。
      // 同じ音が いくつも あれば、ねらった高さに いちばん近いものを その音とする（ほかは よけいな音）
      const best = new Map();
      order.forEach((buf, j) => {
        const x = seg(buf, kind), sr = buf.sampleRate;
        const note = order.length === expected ? mk.low + j : identify(x, sr, mk.low, mk.high);
        // すぐに止めた ノードは 鳴っていないので のぞく
        const heard = nodesOf.get(buf).filter(p => p.node.__stoppedAt === undefined || p.node.__stoppedAt - p.t > 30);
        const rate = heard.length ? heard[0].node.playbackRate.value : 1;
        const item = {
          note,
          cents: cents(x, sr, freq(note)) + 1200 * Math.log2(rate),
          rateOk: heard.every(p => Math.abs(p.node.playbackRate.value - 1) < 1e-6),
        };
        const b = best.get(note);
        if (!b || Math.abs(item.cents) < Math.abs(b.cents)) best.set(note, item);
      });
      const items = [...best.values()];
      const extra = order.length - items.length, missing = expected - items.length;
      let worst = { cents: 0, note: mk.low }, rateBad = 0, bad = 0;
      for (const it of items) {
        if (!it.rateOk) rateBad++;
        if (Math.abs(it.cents) > tolerance || !it.rateOk) bad++;
        if (Math.abs(it.cents) > Math.abs(worst.cents)) worst = it;
      }
      if (bad || missing) ok = false;
      // スマホの たて でも 1 行に おさまるように みじかく（問題が あるときだけ 数を ならべる）
      let line = `${mk.name.padEnd(8)} ${items.length}/${expected} さいだい ${Math.abs(worst.cents).toFixed(1)} セント（${noteName(worst.note)}）`;
      if (rateBad) line += ` playbackRate≠1: ${rateBad}`;
      if (bad) line += ` ずれ: ${bad}`;
      if (missing) line += ` たりない音: ${missing}`;
      if (extra) line += ` よけいな音: ${extra}`;
      lines.push(line);
    });
    lines.push(ok ? `OK: ぜんぶの音が ±${tolerance} セント以内、playbackRate は すべて 1`
      : `NG: ±${tolerance} セントより ずれた音、playbackRate が 1 でない音、または 鳴らなかった音が あります`);
    return { ok, text: lines.join("\n") };
  }
  window.piamoPitchReport = (tolerance = TOLERANCE) => report(tolerance).text;
})();
