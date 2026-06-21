// ReSume Chrome Extension - background.js
// Maintains a persistent native messaging connection to ReSume.NativeHost.exe
// which relays to ReSume.exe via Named Pipe.

let port = null;
let pendingCapture = null;  // { resolve, reject } for current in-flight capture

// ── Connection management ────────────────────────────────────────────────────

function connect() {
  if (port) {
    try { port.disconnect(); } catch (e) {}
    port = null;
  }

  try {
    port = chrome.runtime.connectNative('com.resume.nativehost');
  } catch (err) {
    console.error('[ReSume] connectNative failed:', err);
    scheduleReconnect();
    return;
  }

  port.onMessage.addListener((msg) => {
    console.log('[ReSume] native msg received:', msg);

    if (!msg || typeof msg !== 'object') return;

    switch (msg.action) {
      case 'capture':
        // ReSume.exe is requesting a tab capture (e.g. triggered from desktop Save button)
        captureAndSend();
        break;

      case 'restore':
        // ReSume.exe is asking us to restore tabs
        if (msg.data) restoreTabs(msg.data, () => console.log('[ReSume] restore done'));
        break;

      default:
        // Could be a response to a capture we sent — resolve pending
        if (pendingCapture) {
          pendingCapture.resolve(msg);
          pendingCapture = null;
        }
    }
  });

  port.onDisconnect.addListener(() => {
    const err = chrome.runtime.lastError?.message || 'unknown reason';
    console.warn('[ReSume] native port disconnected:', err);
    port = null;
    pendingCapture?.reject(new Error('Native port disconnected: ' + err));
    pendingCapture = null;
    scheduleReconnect();
  });

  console.log('[ReSume] native port connected');
}

function scheduleReconnect() {
  setTimeout(connect, 5000);
}

// Start connection immediately
connect();

// ── Internal message API (popup.js → background.js) ─────────────────────────

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.action === 'ping') {
    sendResponse({ connected: port !== null });
    return false;
  }

  if (message.action === 'capture') {
    captureAndSend()
      .then((result) => sendResponse({ success: true, result }))
      .catch((err)   => sendResponse({ success: false, error: err.message }));
    return true; // keep channel open for async response
  }

  if (message.action === 'restore') {
    restoreTabs(message.data, () => sendResponse({ success: true }));
    return true;
  }
});

// ── Tab capture ──────────────────────────────────────────────────────────────

/**
 * Collect all windows + tabs and send them to ReSume via the native port.
 * Returns a promise that resolves when ReSume acknowledges.
 */
async function captureAndSend() {
  if (!port) throw new Error('Not connected to ReSume. Is ReSume.exe running?');

  const windows = await new Promise((resolve) =>
    chrome.windows.getAll({ populate: true }, resolve)
  );

  // Optionally enrich tabs with tab group info
  let groupMap = {};
  try {
    const groups = await new Promise((resolve) =>
      chrome.tabGroups.query({}, resolve)
    );
    for (const g of groups) {
      groupMap[g.id] = { title: g.title, color: g.color };
    }
  } catch (e) { /* tabGroups API may not be available */ }

  const data = windows.map(win => ({
    id:       win.id,
    focused:  win.focused,
    state:    win.state,
    left:     win.left   || 0,
    top:      win.top    || 0,
    width:    win.width  || 1280,
    height:   win.height || 800,
    incognito: win.incognito,
    tabs: (win.tabs || []).map(tab => {
      const group = groupMap[tab.groupId] || {};
      return {
        index:      tab.index,
        url:        tab.url,
        title:      tab.title,
        pinned:     tab.pinned,
        muted:      tab.mutedInfo?.muted || false,
        active:     tab.active,
        groupId:    tab.groupId >= 0 ? tab.groupId : -1,
        groupTitle: group.title || null,
        groupColor: group.color || null
      };
    })
  }));

  return new Promise((resolve, reject) => {
    pendingCapture = { resolve, reject };

    // Timeout: if ReSume doesn't respond in 10s, reject
    const timer = setTimeout(() => {
      if (pendingCapture) {
        pendingCapture.reject(new Error('Capture timeout'));
        pendingCapture = null;
      }
    }, 10000);

    // Override resolve to also clear timer
    const origResolve = pendingCapture.resolve;
    pendingCapture.resolve = (val) => { clearTimeout(timer); origResolve(val); };

    try {
      port.postMessage({ action: 'capture', data });
    } catch (err) {
      clearTimeout(timer);
      pendingCapture = null;
      reject(err);
    }
  });
}

// ── Tab restore ──────────────────────────────────────────────────────────────

function restoreTabs(windowsData, callback) {
  if (!windowsData || windowsData.length === 0) {
    callback?.();
    return;
  }

  let completed = 0;
  const total   = windowsData.length;

  windowsData.forEach(winData => {
    const urls = (winData.tabs || [])
      .map(t => t.url)
      .filter(u => u && !u.startsWith('chrome://') && !u.startsWith('chrome-extension://'));

    if (urls.length === 0) urls.push('chrome://newtab/');

    const createParams = {
      url:      urls,
      focused:  winData.focused || false,
      incognito: winData.incognito || false
    };

    // Only set position/size if they look valid
    if (winData.left  >= 0) createParams.left   = winData.left;
    if (winData.top   >= 0) createParams.top    = winData.top;
    if (winData.width  > 0) createParams.width  = winData.width;
    if (winData.height > 0) createParams.height = winData.height;
    if (winData.state)      createParams.state  = winData.state;

    chrome.windows.create(createParams, () => {
      completed++;
      if (completed === total) callback?.();
    });
  });
}
