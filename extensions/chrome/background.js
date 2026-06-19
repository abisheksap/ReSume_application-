let port = null;
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.action === 'capture') { captureTabs(sendResponse); return true; }
  if (message.action === 'restore') { restoreTabs(message.data, sendResponse); return true; }
});

function captureTabs(callback) {
  chrome.windows.getAll({ populate: true }, (windows) => {
    const data = windows.map(win => ({
      id: win.id, focused: win.focused, state: win.state,
      left: win.left, top: win.top, width: win.width, height: win.height, incognito: win.incognito,
      tabs: win.tabs.map(tab => ({
        url: tab.url, title: tab.title, pinned: tab.pinned,
        muted: tab.mutedInfo?.muted || false, groupId: tab.groupId, index: tab.index
      }))
    }));
    sendNativeMessage({ action: 'capture', data: data }, callback);
  });
}

function restoreTabs(sessionData, callback) {
  let done = 0;
  sessionData.forEach(winData => {
    chrome.windows.create({
      url: winData.tabs.map(t => t.url), focused: winData.focused,
      left: winData.left, top: winData.top, width: winData.width, height: winData.height, incognito: winData.incognito
    }, () => { if (++done === sessionData.length) callback({ success: true }); });
  });
  if (sessionData.length === 0) callback({ success: true });
}

function sendNativeMessage(payload, callback) {
  port = chrome.runtime.connectNative('com.resume.nativehost');
  port.onMessage.addListener((response) => { callback(response); port.disconnect(); });
  port.onDisconnect.addListener(() => console.log('Disconnected'));
  port.postMessage(payload);
}