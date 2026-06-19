// ReSume Chrome Extension - Persistent long-lived port
let port = null;

// Connect (or reconnect) the native messaging port
function connect() {
    port = chrome.runtime.connectNative('com.resume.nativehost');
    port.onMessage.addListener((msg) => {
        console.log('ReSume native message received:', msg);
        // If the app sends back responses, handle them here.
    });
    port.onDisconnect.addListener(() => {
        console.log('Native port disconnected. Reconnecting in 2s...');
        port = null;
        setTimeout(connect, 2000);
    });
}

// Start the connection immediately
connect();

// Listen for messages from the popup (or other internal scripts)
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.action === 'capture') {
        captureTabs(sendResponse);
        return true;
    } else if (message.action === 'restore') {
        restoreTabs(message.data, sendResponse);
        return true;
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
                active: tab.active,          // added for restore
                groupTitle: tab.groupTitle,  // added for restore
                groupColor: tab.groupColor   // added for restore
            }))
        }));
        // Send the capture data through the persistent port
        const payload = { action: 'capture', data: data };
        if (port) {
            port.postMessage(payload);
            callback({ success: true });
        } else {
            callback({ success: false, error: 'Not connected' });
        }
    });
}

function restoreTabs(sessionData, callback) {
    let count = 0;
    if (sessionData.length === 0) {
        callback({ success: true });
        return;
    }
    sessionData.forEach(winData => {
        chrome.windows.create({
            url: winData.tabs.map(t => t.url),
            focused: winData.focused,
            left: winData.left,
            top: winData.top,
            width: winData.width,
            height: winData.height,
            incognito: winData.incognito
        }, () => {
            count++;
            if (count === sessionData.length) {
                callback({ success: true });
            }
        });
    });
}