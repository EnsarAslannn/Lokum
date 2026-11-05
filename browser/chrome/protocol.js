(() => {
  "use strict";
  function http(value) {
    if (typeof value !== "string" || value.length > 32768) return null;
    try { const url = new URL(value); return /^https?:$/.test(url.protocol) && !url.username && !url.password ? url.href : null; } catch { return null; }
  }
  function request(message, sender, extensionId) {
    if (!message || sender?.id !== extensionId) throw new Error("Invalid extension sender.");
    if (message.operation === "ping") {
      if (sender.url !== `chrome-extension://${extensionId}/popup.html`) throw new Error("Invalid setup request.");
      return {version:1,operation:"ping"};
    }
    const senderUrl = http(sender.url);
    if (message.operation !== "open" || !senderUrl || !Number.isInteger(sender.tab?.id) || !["image","video"].includes(message.kind)) throw new Error("Invalid media request.");
    // Chrome's isolated-world sender URL can remain the original document URL
    // after history.pushState (YouTube Shorts/feed navigation). Accept the live
    // document location only within Chrome's authenticated frame origin.
    const live = http(message.pageUrl);
    const pageUrl = live && new URL(live).origin === new URL(senderUrl).origin ? live : senderUrl;
    let url;
    if (message.kind === "video" && youtubeHost(new URL(pageUrl).hostname)) {
      url = youtubeVideo(message.videoPageUrl) || youtubeVideo(pageUrl);
      if (!url) throw new Error("YouTube videosunu veya Shorts’u kendi sayfasında açıp tekrar dene.");
    } else {
      url = http(message.url) || (message.kind === "video" && (!message.url || (typeof message.url === "string" && message.url.startsWith("blob:"))) ? pageUrl : null);
    }
    if (!url) throw new Error("This image address is not exposed by the website. Save the image and open it in Lokum.");
    return {version:1,operation:"open",kind:message.kind,url,pageUrl};
  }
  function youtubeHost(host) { return ["youtube.com","www.youtube.com","m.youtube.com","music.youtube.com","youtu.be"].includes(host); }
  function youtubeVideo(value) {
    const address = http(value); if (!address) return null;
    const url = new URL(address); if (!youtubeHost(url.hostname)) return null;
    const parts = url.pathname.split("/").filter(Boolean);
    const id = url.hostname === "youtu.be" ? parts[0] : ["shorts","embed","live"].includes(parts[0]) ? parts[1] : url.searchParams.get("v");
    return /^[A-Za-z0-9_-]{11}$/.test(id || "") ? `https://www.youtube.com/watch?v=${id}` : null;
  }
  globalThis.LokumBrowserProtocol = Object.freeze({host:"com.lokum.media",request});
})();
