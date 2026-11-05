(() => {
  "use strict";
  if (globalThis.__lokumMediaShortcut) return;
  globalThis.__lokumMediaShortcut = true;

  function mediaAt(event) {
    const path = event.composedPath().filter(node => node instanceof Element);
    const direct = path.find(node => node.matches("img,video"));
    if (direct) return direct;
    if (path.some(node => node.matches("button,input,textarea,select,[contenteditable=true]"))) return null;
    // A custom player often puts a transparent overlay over its video. Require
    // a shared player-sized container so a dialog over an unrelated video is
    // not mistaken for a click on that video.
    const hits = document.elementsFromPoint(event.clientX, event.clientY);
    for (const video of hits.filter(node => node.matches("video"))) {
      const rect = video.getBoundingClientRect();
      if (rect.width < 1 || rect.height < 1) continue;
      if (path.some(node => !node.matches("html,body") && node.contains(video) &&
        node.getBoundingClientRect().width * node.getBoundingClientRect().height <= rect.width * rect.height * 4)) return video;
    }
    return null;
  }

  function feedback(message) {
    document.getElementById("lokum-media-feedback")?.remove();
    const host = document.createElement("div"); host.id = "lokum-media-feedback";
    host.style.cssText = "position:fixed;right:20px;bottom:20px;z-index:2147483647";
    const shadow = host.attachShadow({mode:"closed"});
    const label = document.createElement("div");
    label.style.cssText = "font:14px/1.5 system-ui;color:#392a35;background:#fff7f4;border:1px solid #d8b5c3;border-radius:16px;padding:14px 18px;max-width:360px;box-shadow:0 4px 24px #392a3526";
    label.textContent = message; shadow.append(label); document.documentElement.append(host);
    setTimeout(() => host.remove(), 7000);
  }

  function videoPage(media) {
    const link = media.closest("a[href]"); if (link) return link.href;
    const card = media.closest("ytd-reel-video-renderer,ytd-rich-item-renderer,ytd-video-renderer,ytd-grid-video-renderer,yt-lockup-view-model");
    if (!card) return null;
    const id = card.getAttribute("video-id") || card.getAttribute("data-video-id");
    if (/^[A-Za-z0-9_-]{11}$/.test(id || "")) return `https://www.youtube.com/watch?v=${id}`;
    return card.querySelector('a[href*="/shorts/"],a[href*="watch?v="]')?.href || null;
  }

  window.addEventListener("click", async event => {
    if (!event.isTrusted || event.button !== 0 || event.detail < 1 || !event.ctrlKey || event.altKey || event.shiftKey || event.metaKey) return;
    const media = mediaAt(event); if (!media) return;
    event.preventDefault(); event.stopImmediatePropagation();
    const kind = media.tagName.toLowerCase() === "video" ? "video" : "image";
    const url = media.currentSrc || media.src || media.querySelector("source[src]")?.src || "";
    try {
      const response = await chrome.runtime.sendMessage({operation:"open",kind,url,pageUrl:location.href,videoPageUrl:kind === "video" ? videoPage(media) : null});
      if (!response?.ok) throw new Error(response?.error || "");
    } catch (error) {
      const tr = navigator.language.toLowerCase().startsWith("tr");
      feedback(error.message || (tr ? "Lokum bağlantısı hazır değil. Uzantı simgesinden kurulumu kontrol et." : "Lokum is not connected. Open the extension to check setup."));
    }
  }, {capture:true, passive:false});
})();
