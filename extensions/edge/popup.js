const saveBtn    = document.getElementById('saveBtn');
const statusText = document.getElementById('statusText');
const dot        = document.getElementById('dot');
const infoText   = document.getElementById('infoText');

function setConnected(connected) {
  if (connected) {
    dot.className       = 'dot connected';
    statusText.textContent = 'Connected to ReSume';
    saveBtn.disabled    = false;
    infoText.textContent   = 'Captures all open tabs across all windows.';
  } else {
    dot.className       = 'dot disconnected';
    statusText.textContent = 'ReSume not running';
    saveBtn.disabled    = true;
    infoText.textContent   = 'Start ReSume.exe to enable tab capture.';
  }
}

function setLoading(loading) {
  if (loading) {
    saveBtn.classList.add('loading');
    saveBtn.disabled = true;
  } else {
    saveBtn.classList.remove('loading');
    saveBtn.disabled = false;
  }
}

// Check connection on open
chrome.runtime.sendMessage({ action: 'ping' }, (response) => {
  if (chrome.runtime.lastError) {
    setConnected(false);
    statusText.textContent = 'Extension error';
    return;
  }
  setConnected(response?.connected === true);
});

// Save button
saveBtn.addEventListener('click', () => {
  setLoading(true);
  statusText.textContent = 'Capturing tabs…';
  dot.className = 'dot';

  chrome.runtime.sendMessage({ action: 'capture' }, (response) => {
    setLoading(false);
    if (chrome.runtime.lastError) {
      statusText.textContent = 'Error: ' + chrome.runtime.lastError.message;
      dot.className = 'dot disconnected';
      return;
    }
    if (response?.success) {
      dot.className          = 'dot connected';
      statusText.textContent = 'Session captured!';
      infoText.textContent   = 'Tabs sent to ReSume successfully.';
    } else {
      dot.className          = 'dot disconnected';
      statusText.textContent = 'Capture failed';
      infoText.textContent   = response?.error || 'Check that ReSume.exe is running.';
    }
  });
});
