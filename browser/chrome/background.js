"use strict";
importScripts("protocol.js");
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  let request;
  try { request = LokumBrowserProtocol.request(message, sender, chrome.runtime.id); }
  catch (error) { reply({ok:false,error:error.message}); return false; }
  chrome.runtime.sendNativeMessage(LokumBrowserProtocol.host, request)
    .then(reply)
    .catch(() => reply({ok:false,error:"Lokum bağlantısı hazır değil. Lokum > Ayarlar > Chrome bağlantısı ile kurulumu tamamla."}));
  return true;
});
