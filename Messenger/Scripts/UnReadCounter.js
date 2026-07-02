/// <summary>
/// Count the number of unread messages and send it to the host application
/// </summary>
setInterval(() => {
    const unread = Array.from(document.querySelectorAll('[role="row"]'))
        .filter(el => el.textContent?.includes('Oläst meddelande:'))
        .length;
    window.chrome.webview.postMessage(JSON.stringify({ unread }));
}, 3000);