document.getElementById('saveBtn').addEventListener('click', () => {
  chrome.runtime.sendMessage({ action: 'capture' }, (r) => alert(r ? 'Sent' : 'Failed'));
});