// ReSume Browser Extension - Persistent Native Port
let port = null;
const pendingCallbacks = new Map();

function connect() {
    if (port) {
        try { port.disconnect(); } catch(e) {}
    }
    port = chrome.runtime.connectNative('com.resume.nativehost');
    port.onMessage.addListener((msg) => {
        console.log('ReSume native msg:', msg);
        // If the message is a response to a pending request, resolve it
        if (msg.action && pendingCallbacks.has(msg.action)) {
            const cb = pendingCallbacks.get(msg.action);
            pendingCallbacks.delete(msg.action);
            cb(msg);
        }
        // Handle unsolicited messages (e.g., restore command from desktop)
        if (msg.action === 'restore') {
            restoreTabs(msg.data, () => console.log('Restore completed'));
        }
    });
    port.onDisconnect.addListener(() => {
        console.log('Native port disconnected. Reconnecting in 3s...');
        port = null;
        setTimeout(connect, 3000);
    });
}

// Start persistent connection
connect();

// Listen for internal messages (popup, etc.)
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.action === 'capture') {
        captureTabs(sendResponse);
        return true;
    } else if (message.action === 'restore') {
        restoreTabs(message.data, sendResponse);
        return true;
    } else if (message.action === 'ping') {
        // Check if native port is alive
        sendResponse({ connected: port !== null });
        return false;
    }
});

function captureTabs(callback) {
    chrome.windows.getAll({ populate: true }, (windows) => {
        const data = windows.map(win => ({
            id: win.id,
            focused: win.focused,
            state: win.state,
            left: win.left,
            top: win.top,
            width: win.width,
            height: win.height,
            incognito: win.incognito,
            tabs: win.tabs.map(tab => ({
                url: tab.url,
                title: tab.title,
                pinned: tab.pinned,
                muted: tab.mutedInfo?.muted || false,
                groupId: tab.groupId,
                index: tab.index,
                active: tab.active,
                groupTitle: tab.groupTitle,
                groupColor: tab.groupColor
            }))
        }));
        // Send via persistent port, store callback for response
        const payload = { action: 'capture', data: data };
        if (port) {
            pendingCallbacks.set('capture', (response) => {
                callback(response);
            });
            port.postMessage(payload);
        } else {
            callback({ success: false, error: 'Not connected to native host. Is ReSume running?' });
        }
    });
}

function restoreTabs(sessionData, callback) {
    if (!sessionData || sessionData.length === 0) {
        callback({ success: true });
        return;
    }
    let count = 0;
    sessionData.forEach(winData => {
        const urls = winData.tabs.map(t => t.url).filter(u => u);
        if (urls.length === 0) urls.push('chrome://newtab/');
        chrome.windows.create({
            url: urls,
            focused: winData.focused,
            left: winData.left,
            top: winData.top,
            width: winData.width,
            height: winData.height,
            incognito: winData.incognito,
            state: winData.state || 'maximized'
        }, () => {
            count++;
            if (count === sessionData.length) {
                callback({ success: true });
            }
        });
    });
}