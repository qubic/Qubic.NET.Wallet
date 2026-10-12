// Reads QR codes from images: pasted (Ctrl+V), dropped, read from the clipboard, or opened from a file.
// Decoding runs locally with jsQR (lib/jsqr); nothing leaves the machine.
window.qbQr = (() => {
    const MAX_SIDE = 3000;

    async function decodeBlob(blob) {
        const url = URL.createObjectURL(blob);
        try {
            const img = new Image();
            await new Promise((resolve, reject) => { img.onload = resolve; img.onerror = reject; img.src = url; });
            const scale = Math.min(1, MAX_SIDE / Math.max(img.naturalWidth, img.naturalHeight));
            const w = Math.max(1, Math.round(img.naturalWidth * scale));
            const h = Math.max(1, Math.round(img.naturalHeight * scale));
            const canvas = document.createElement('canvas');
            canvas.width = w;
            canvas.height = h;
            const ctx = canvas.getContext('2d', { willReadFrequently: true });
            ctx.fillStyle = 'white';
            ctx.fillRect(0, 0, w, h);
            ctx.drawImage(img, 0, 0, w, h);
            const data = ctx.getImageData(0, 0, w, h);
            const code = jsQR(data.data, w, h, { inversionAttempts: 'attemptBoth' });
            return code && code.data ? code.data : null;
        } catch {
            return null;
        } finally {
            URL.revokeObjectURL(url);
        }
    }

    function imageFile(dataTransfer) {
        if (!dataTransfer) return null;
        for (const item of dataTransfer.items || []) {
            if (item.kind === 'file' && item.type.startsWith('image/')) return item.getAsFile();
        }
        for (const file of dataTransfer.files || []) {
            if (file.type.startsWith('image/')) return file;
        }
        return null;
    }

    function hasFiles(dataTransfer) {
        return !!dataTransfer && Array.from(dataTransfer.types || []).includes('Files');
    }

    // Listens for pasted or dropped images on the button's parent (the field group).
    function attach(button, dotNetRef) {
        const scope = button && button.parentElement;
        if (!scope) return;
        const report = async (file) => {
            const text = await decodeBlob(file);
            await dotNetRef.invokeMethodAsync('OnImageDecoded', text);
        };
        const onPaste = (e) => {
            const file = imageFile(e.clipboardData);
            if (!file) return; // plain text paste stays untouched
            e.preventDefault();
            report(file);
        };
        const onDragOver = (e) => { if (hasFiles(e.dataTransfer)) e.preventDefault(); };
        const onDrop = (e) => {
            const file = imageFile(e.dataTransfer);
            if (!file) return;
            e.preventDefault();
            report(file);
        };
        scope.addEventListener('paste', onPaste);
        scope.addEventListener('dragover', onDragOver);
        scope.addEventListener('drop', onDrop);
        button._qbQrDetach = () => {
            scope.removeEventListener('paste', onPaste);
            scope.removeEventListener('dragover', onDragOver);
            scope.removeEventListener('drop', onDrop);
        };
    }

    function detach(button) {
        if (button && button._qbQrDetach) { button._qbQrDetach(); delete button._qbQrDetach; }
    }

    // Returns { status: 'ok' | 'noqr' | 'noimage' | 'unsupported', text }.
    async function fromClipboard() {
        if (!navigator.clipboard || !navigator.clipboard.read) return { status: 'unsupported', text: null };
        let items;
        try { items = await navigator.clipboard.read(); }
        catch { return { status: 'unsupported', text: null }; }
        for (const item of items) {
            const type = item.types.find(t => t.startsWith('image/'));
            if (!type) continue;
            const text = await decodeBlob(await item.getType(type));
            return { status: text ? 'ok' : 'noqr', text };
        }
        return { status: 'noimage', text: null };
    }

    // Opens a file chooser for an image. Returns { status: 'ok' | 'noqr' | 'cancelled', text }.
    function fromFile() {
        return new Promise((resolve) => {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = 'image/*';
            input.onchange = async () => {
                const file = input.files && input.files[0];
                if (!file) { resolve({ status: 'cancelled', text: null }); return; }
                const text = await decodeBlob(file);
                resolve({ status: text ? 'ok' : 'noqr', text });
            };
            input.oncancel = () => resolve({ status: 'cancelled', text: null });
            input.click();
        });
    }

    return { attach, detach, fromClipboard, fromFile };
})();
