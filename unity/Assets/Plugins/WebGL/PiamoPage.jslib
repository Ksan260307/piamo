// Piamo：ブラウザとのやりとり（日付・画面のはし・タブをとじる前の保存・ふるえる）
mergeInto(LibraryManager.library, {
  // その土地の きょうの日付（yyyymmdd）。C# の DateTime.Now が UTC になるブラウザがあるので、Date から読む
  PiamoLocalDate: function () {
    try {
      var d = new Date();
      return d.getFullYear() * 10000 + (d.getMonth() + 1) * 100 + d.getDate();
    } catch (e) { return 0; }
  },

  // スマホのノッチなど、画面の端の隠れる部分の幅（画面に対する割合）。side: 0 左 1 上 2 右 3 下
  PiamoSafeInset: function (side) {
    try {
      var el = window.__piamoSafeEl;
      if (!el) {
        el = document.createElement("div");
        el.style.cssText = "position:fixed;left:0;top:0;width:0;height:0;visibility:hidden;pointer-events:none;" +
          "padding-left:env(safe-area-inset-left);padding-top:env(safe-area-inset-top);" +
          "padding-right:env(safe-area-inset-right);padding-bottom:env(safe-area-inset-bottom);";
        document.body.appendChild(el);
        window.__piamoSafeEl = el;
      }
      var cs = window.getComputedStyle(el);
      var v = [cs.paddingLeft, cs.paddingTop, cs.paddingRight, cs.paddingBottom][side];
      var px = parseFloat(v) || 0;
      var total = side % 2 == 0 ? window.innerWidth : window.innerHeight;
      return total > 0 ? px / total : 0;
    } catch (e) { return 0; }
  },

  // タブをとじる・切りかえる前に、GameObject「App」へ知らせる（保存する・音を止める）
  PiamoRegisterPageEvents: function () {
    if (typeof document === "undefined" || window.__piamoPageEvents) return;
    window.__piamoPageEvents = true;
    var send = function (method) {
      try { SendMessage("App", method); } catch (e) { }
    };
    document.addEventListener("visibilitychange", function () {
      send(document.hidden ? "OnPageHidden" : "OnPageVisible");
    });
    window.addEventListener("pagehide", function () { send("OnPageHidden"); });
    window.addEventListener("beforeunload", function () { send("OnPageHidden"); });
  },

  PiamoVibrate: function (ms) {
    try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) { }
  },
});
