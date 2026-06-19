const saveBtn = document.getElementById('saveBtn');
const statusEl = document.getElementById('status');

// Check connection on popup open
chrome.runtime.sendMessage({ action: 'ping' }, (response) => {
    if (chrome.runtime.lastError) {
        statusEl.textContent = 'Extension not ready';
    } else if (response && response.connected) {
        statusEl.textContent = 'Connected';
        saveBtn.disabled = false;
    } else {
        statusEl.textContent = 'Not connected (start ReSume.exe)';
    }
});

saveBtn.addEventListener('click', () => {
    saveBtn.disabled = true;
    saveBtn.textContent = 'Saving…';
    statusEl.textContent = 'Saving session...';
    chrome.runtime.sendMessage({ action: 'capture' }, (response) => {
        if (chrome.runtime.lastError) {
            statusEl.textContent = 'Error: ' + chrome.runtime.lastError.message;
            saveBtn.disabled = false;
            saveBtn.textContent = 'Save Session Now';
            return;
        }
        if (response && response.success) {
            statusEl.textContent = 'Session captured!';
        } else {
            statusEl.textContent = 'Capture failed. ' + (response?.error || '');
        }
        saveBtn.disabled = false;
        saveBtn.textContent = 'Save Session Now';
    });
});