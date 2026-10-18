/**
 * App utilities the Blazor components call through JS interop.
 *
 * These used to be an inline block in index.html. They live in a file because the page's
 * Content-Security-Policy runs only scripts from this origin: no inline blocks and no inline
 * event handlers. Wire handlers up here with addEventListener, never onclick="...".
 */

// The selection in the markdown editor's textarea, with the text as it is right now.
// MudTextField puts the id on the <textarea> itself; the lookup also accepts a container.
// The text comes back too because the bound value is debounced and can lag what was typed,
// and the offsets only mean something against the text they were taken from.
window.markdownEditorGetSelection = function (id) {
    const element = document.getElementById(id);
    const textarea = element?.tagName === 'TEXTAREA' ? element : element?.querySelector('textarea');
    if (!textarea) return { start: -1, end: -1, text: null };

    return {
        start: textarea.selectionStart,
        end: textarea.selectionEnd,
        text: textarea.value
    };
};

// Barcode scanner state
window.barcodeScanner = {
    scanner: null,
    containerId: 'barcode-scanner-container',
    isScanning: false
};

// Check if barcode scanning is available (camera access)
window.barcodeScannerIsAvailable = function () {
    return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
};

// Start barcode scanner and return a promise that resolves with the barcode
window.barcodeScannerStart = function (dotnetHelper) {
    return new Promise((resolve, reject) => {
        // Create scanner container if it doesn't exist
        let container = document.getElementById(window.barcodeScanner.containerId);
        if (!container) {
            container = document.createElement('div');
            container.id = window.barcodeScanner.containerId;
            container.style.cssText = 'position:fixed;top:0;left:0;right:0;bottom:0;z-index:9999;background:rgba(0,0,0,0.9);display:flex;flex-direction:column;align-items:center;justify-content:center;';
            container.innerHTML = `
                <div style="background:white;border-radius:12px;padding:20px;max-width:400px;width:90%;text-align:center;">
                    <div id="barcode-reader" style="width:100%;"></div>
                    <button id="barcode-cancel" style="margin-top:16px;padding:10px 24px;background:#666;color:white;border:none;border-radius:6px;cursor:pointer;font-size:16px;">Cancel</button>
                </div>
            `;
            document.body.appendChild(container);
        }
        container.style.display = 'flex';

        // Setup cancel button
        document.getElementById('barcode-cancel').onclick = function () {
            window.barcodeScannerStop();
            resolve(null);
        };

        // Initialize scanner
        window.barcodeScanner.scanner = new Html5Qrcode('barcode-reader');
        window.barcodeScanner.isScanning = true;

        const config = {
            fps: 10,
            qrbox: { width: 250, height: 100 },
            aspectRatio: 1.777778
        };

        window.barcodeScanner.scanner.start(
            { facingMode: 'environment' },
            config,
            (decodedText, decodedResult) => {
                // Barcode found
                window.barcodeScannerStop();
                resolve(decodedText);
            },
            (errorMessage) => {
                // Scan error (ignore, keep scanning)
            }
        ).catch((err) => {
            console.error('Failed to start scanner:', err);
            window.barcodeScannerStop();
            reject(err.message || 'Failed to start camera');
        });
    });
};

// Stop barcode scanner
window.barcodeScannerStop = function () {
    if (window.barcodeScanner.scanner && window.barcodeScanner.isScanning) {
        window.barcodeScanner.scanner.stop().catch(err => console.log('Stop error:', err));
        window.barcodeScanner.isScanning = false;
    }
    const container = document.getElementById(window.barcodeScanner.containerId);
    if (container) {
        container.style.display = 'none';
    }
};

// Download a file from a base64 data URL
window.downloadBase64File = function (dataUrl, filename) {
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
};

// The error bar in index.html. Wired up here because the page policy blocks inline handlers.
document.getElementById('blazor-error-reload')?.addEventListener('click', function (e) {
    e.preventDefault();
    location.reload();
});
document.getElementById('blazor-error-dismiss')?.addEventListener('click', function (e) {
    e.preventDefault();
    document.getElementById('blazor-error-ui').style.display = 'none';
});
