"use strict";
document.querySelector("#check").addEventListener("click", async () => {
  const status = document.querySelector("#status"); status.textContent = "Bağlantı kontrol ediliyor…";
  try {
    const reply = await chrome.runtime.sendMessage({operation:"ping"});
    status.textContent = reply?.ok ? "Bağlantı hazır. Web sayfasını yenileyip bir görsel veya videoda Ctrl + sol tık yap." : reply?.error || "Lokum bağlantısı kurulamadı.";
  } catch { status.textContent = "Lokum → Ayarlar → Chrome bağlantısı ile kurulumu tamamla."; }
});
