"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const crypto = require("node:crypto");
const root = path.resolve(__dirname,"..");
const context = vm.createContext({URL});
vm.runInContext(fs.readFileSync(path.join(root,"browser/chrome/protocol.js"),"utf8"),context);
const protocol = context.LokumBrowserProtocol;
const manifest = JSON.parse(fs.readFileSync(path.join(root,"browser/chrome/manifest.json"),"utf8"));
const id = crypto.createHash("sha256").update(Buffer.from(manifest.key,"base64")).digest("hex").slice(0,32).replace(/[0-9a-f]/g,c=>String.fromCharCode(97+parseInt(c,16)));
assert.equal(id,"djllfhjpfpmpcpfhnmnciacikoklceln");
const sender={id,tab:{id:4},url:"https://www.youtube.com/watch?v=aqz-KE-bpKQ"};
const image=protocol.request({operation:"open",kind:"image",url:"https://i.ytimg.com/vi/aqz-KE-bpKQ/hqdefault.jpg"},sender,id);
assert.equal(image.kind,"image");assert.equal(image.url,"https://i.ytimg.com/vi/aqz-KE-bpKQ/hqdefault.jpg");
const video=protocol.request({operation:"open",kind:"video",url:"blob:https://www.youtube.com/private-id",pageUrl:"https://wrong.example/page"},sender,id);
assert.equal(video.kind,"video");assert.equal(video.url,sender.url);assert.equal(video.pageUrl,sender.url);
assert.equal(protocol.request({operation:"open",kind:"video",url:"https://cdn.example/stream?signature=keep"},{...sender,url:"https://fixture.example/page"},id).url,"https://cdn.example/stream?signature=keep");
for(const invalid of [
  [{operation:"open",kind:"file",url:"https://example.com/a"},sender],
  [{operation:"open",kind:"image",url:"javascript:alert(1)"},sender],
  [{operation:"open",kind:"image",url:"https://name:secret@example.com/a"},sender],
  [{operation:"open",kind:"image",url:"https://example.com/a"},{...sender,id:"other"}],
  [{operation:"open",kind:"image",url:"https://example.com/a"},{...sender,url:"file:///private/file"}],
  [{operation:"execute",url:"https://example.com/a"},sender],
  [{operation:"ping"},sender],
]) assert.throws(()=>protocol.request(...invalid,id));
assert.equal(protocol.request({operation:"ping"},{id,url:`chrome-extension://${id}/popup.html`},id).operation,"ping");
const shorts=protocol.request({operation:"open",kind:"video",url:"blob:https://www.youtube.com/player-session",pageUrl:"https://www.youtube.com/shorts/X31goIHSY3c"},{...sender,url:"https://www.youtube.com/"},id);
assert.equal(shorts.url,"https://www.youtube.com/watch?v=X31goIHSY3c","A Shorts SPA navigation must resolve the currently clicked video instead of the tab's original YouTube homepage");
const stream=protocol.request({operation:"open",kind:"video",url:"https://rr1.googlevideo.com/videoplayback?signature=temporary",pageUrl:"https://www.youtube.com/shorts/X31goIHSY3c"},sender,id);
assert.equal(stream.url,"https://www.youtube.com/watch?v=X31goIHSY3c","A YouTube player stream must resolve through its video page rather than a temporary media segment");
assert.throws(()=>protocol.request({operation:"open",kind:"video",url:"blob:https://www.youtube.com/session",pageUrl:"https://foreign.example/shorts/X31goIHSY3c"},{...sender,url:"https://www.youtube.com/"},id));
const card=protocol.request({operation:"open",kind:"video",url:"blob:https://www.youtube.com/session",videoPageUrl:"https://www.youtube.com/shorts/X31goIHSY3c"},{...sender,url:"https://www.youtube.com/"},id);
assert.equal(card.url,"https://www.youtube.com/watch?v=X31goIHSY3c");
console.log("PASS Chrome request boundary: images, video blobs, signed URLs, sender and setup validation");
