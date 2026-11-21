// Мост между HTML и Android: сеть без CORS, сохранение в «Загрузки»,
// выбор файлов и цвет системных панелей.
(function () {
  var bridge = window.AndroidBridge;
  if (!bridge) return;

  var seq = 0;
  var waiting = {};
  var nativeFetch = window.fetch ? window.fetch.bind(window) : null;

  var VIDEO_RE = /\.(mp4|m4v|webm|mov)(\?|#|$)|tiktokcdn|byteoversea|akamaized\.net|ibytedtos|\/video\/tos-/i;
  var BINARY_RE = /^(video\/|image\/)|application\/octet-stream/i;

  window.__androidHttp = function (id, ok, status, text, ctype) {
    var job = waiting[id];
    if (!job) return;
    delete waiting[id];
    if (!ok) {
      job.reject(new Error("HTTP " + (status || "error")));
      return;
    }
    if (ctype && BINARY_RE.test(ctype)) {
      job.reject(new Error("binary"));
      return;
    }
    job.resolve(new Response(text || "", {
      status: status || 200,
      headers: { "Content-Type": ctype || "text/plain; charset=utf-8" }
    }));
  };

  function nativeGet(url, init) {
    return new Promise(function (resolve, reject) {
      var id = "r" + (++seq) + "_" + Date.now();
      waiting[id] = { resolve: resolve, reject: reject };
      try {
        bridge.httpGet(id, url, (init && init.method) || "GET", (init && init.body) || "");
      } catch (e) {
        delete waiting[id];
        reject(e);
      }
    });
  }

  window.fetch = function (input, init) {
    var url = "";
    try {
      url = typeof input === "string" ? input : (input && input.url) || "";
    } catch (e) {}
    if (!/^https?:/i.test(url)) {
      return nativeFetch ? nativeFetch(input, init) : Promise.reject(new Error("нет сети"));
    }
    if (VIDEO_RE.test(url)) {
      return Promise.reject(new Error("видео скачивается отдельно"));
    }
    return nativeGet(url, init).catch(function () {
      return nativeFetch ? nativeFetch(input, init) : Promise.reject(new Error("нет сети"));
    });
  };

  function saveBlobUrl(url, name) {
    var source = nativeFetch ? nativeFetch(url) : Promise.reject(new Error("нет сети"));
    source
      .then(function (r) { return r.blob(); })
      .then(function (blob) {
        var mime = blob.type || (/\.gif$/i.test(name) ? "image/gif" : "video/mp4");
        var id = "s" + (++seq);
        bridge.saveBegin(id, name, mime);
        var reader = new FileReader();
        reader.onerror = function () {
          bridge.saveFail(id);
          bridge.toast("Не удалось сохранить файл");
        };
        reader.onload = function () {
          try {
            var bytes = new Uint8Array(reader.result);
            var chunk = 512 * 1024;
            for (var offset = 0; offset < bytes.length; offset += chunk) {
              var part = bytes.subarray(offset, Math.min(offset + chunk, bytes.length));
              var binary = "";
              for (var i = 0; i < part.length; i += 8192) {
                binary += String.fromCharCode.apply(null, part.subarray(i, i + 8192));
              }
              bridge.saveChunk(id, btoa(binary));
            }
            bridge.saveEnd(id);
          } catch (e) {
            bridge.saveFail(id);
            bridge.toast("Не удалось сохранить файл");
          }
        };
        reader.readAsArrayBuffer(blob);
      })
      .catch(function () {
        bridge.toast("Не удалось сохранить файл");
      });
  }

  document.addEventListener("click", function (event) {
    var node = event.target;
    while (node && node !== document && node.tagName !== "A") node = node.parentNode;
    if (!node || node.tagName !== "A") return;
    var href = node.getAttribute("href") || "";
    var name = node.getAttribute("download") || "FlaminGif";
    if (/^blob:/i.test(href)) {
      event.preventDefault();
      saveBlobUrl(href, name);
      return;
    }
    if (/^https?:/i.test(href)) {
      event.preventDefault();
      if (VIDEO_RE.test(href)) bridge.downloadUrl(href, name);
      else bridge.openUrl(href);
    }
  }, true);

  window.__androidStatus = function (text, kind) {
    var el = document.getElementById("downloadStatus");
    if (!el) return;
    el.textContent = text;
    el.className = "note" + (kind ? " " + kind : "");
  };

  function syncBars() {
    try {
      var bg = getComputedStyle(document.documentElement).getPropertyValue("--bg").trim();
      if (bg) bridge.setThemeColor(bg);
    } catch (e) {}
  }

  document.addEventListener("DOMContentLoaded", function () {
    syncBars();
    setTimeout(syncBars, 150);
    try {
      new MutationObserver(syncBars).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ["data-theme"]
      });
    } catch (e) {}
  });
  window.addEventListener("load", syncBars);
})();
