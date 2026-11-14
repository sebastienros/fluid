// Browser helpers of the playground: the URL fragment, the clipboard, the theme and the Tab key in the editors.

export function readHash() {
    return location.hash;
}

export function writeHash(hash) {
    history.replaceState(null, '', '#' + hash);
}

export function currentUrl() {
    return location.href;
}

export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

export function isWide() {
    return matchMedia('(min-width: 1100px)').matches;
}

export function markReady() {
    const measure = performance.measure('fluid-ready', 'fluid-start');
    return measure.duration;
}

// The theme is shared with the documentation site: same origin, same storage key.
export function toggleTheme() {
    const root = document.documentElement;
    const current = root.dataset.theme || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    const next = current === 'dark' ? 'light' : 'dark';
    root.dataset.theme = next;
    try { localStorage.setItem('fluid-theme', next); } catch { }
}

// Tab inserts two spaces in the editors instead of leaving them, Escape then Tab leaves them. insertText keeps the
// undo history and raises the input event that Blazor listens to.
let escaped = false;
document.addEventListener('keydown', event => {
    if (!event.target.matches?.('textarea.code')) {
        return;
    }

    if (event.key === 'Tab' && !event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey && !escaped) {
        event.preventDefault();
        document.execCommand('insertText', false, '  ');
    }

    escaped = event.key === 'Escape';
});

// The highlighted code under an editor's textarea follows its scroll through CSS variables on the editor element: Blazor
// replaces the pre on every input, and the variables survive that, unlike a scroll position set on the pre.
export function attachEditor(editor) {
    const textarea = editor.querySelector('textarea');
    const sync = () => {
        editor.style.setProperty('--scroll-x', textarea.scrollLeft + 'px');
        editor.style.setProperty('--scroll-y', textarea.scrollTop + 'px');
    };
    textarea.addEventListener('scroll', sync, { passive: true });
    textarea.addEventListener('input', sync);
    sync();
}
