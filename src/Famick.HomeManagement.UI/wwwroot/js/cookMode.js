// Cook mode (recipes): keeps the screen awake while it is open, and sounds a timer when it ends.
// Loaded as a module by Pages/Recipes/RecipeCook.razor. Every feature here degrades quietly: a
// browser without the Screen Wake Lock API, or one that refuses it, just lets the screen sleep.

let wakeLock = null;
let wantAwake = false;
let page = null;

// Tells the page whether the screen is being kept on, for its "Screen stays on" control.
function report(awake) {
    page?.invokeMethodAsync('OnWakeLockChanged', awake).catch(() => { /* page gone */ });
}

async function requestWakeLock() {
    if (!('wakeLock' in navigator) || wakeLock) return !!wakeLock;
    try {
        const lock = await navigator.wakeLock.request('screen');
        lock.addEventListener('release', () => {
            if (wakeLock === lock) wakeLock = null;
            report(false);
        });
        wakeLock = lock;
        report(true);
        return true;
    } catch {
        // Refused, e.g. on low battery or in a hidden tab.
        wakeLock = null;
        return false;
    }
}

// The browser releases the lock whenever the tab is hidden; take it again on return.
function onVisibilityChange() {
    if (wantAwake && document.visibilityState === 'visible') requestWakeLock();
}

// Whether this browser can keep the screen on here: it needs the API, and the API needs a secure
// page (https, or localhost).
export function canKeepAwake() {
    return 'wakeLock' in navigator;
}

/** Keeps the screen on until allowSleep. Returns whether the browser agreed. */
export async function keepAwake(dotnet) {
    page = dotnet ?? page;
    wantAwake = true;
    document.addEventListener('visibilitychange', onVisibilityChange);
    return await requestWakeLock();
}

export async function allowSleep() {
    wantAwake = false;
    document.removeEventListener('visibilitychange', onVisibilityChange);
    const lock = wakeLock;
    wakeLock = null;
    try { await lock?.release(); } catch { /* already released */ }
}

let keyHandler = null;

// The arrow keys and Esc, on the whole document: focus can't be relied on to stay inside cook mode,
// e.g. once a timer's focused button is removed when it ends.
export function listenForKeys(dotnet) {
    stopListeningForKeys();
    keyHandler = (e) => {
        if (!['ArrowLeft', 'ArrowRight', 'Escape'].includes(e.key) || e.defaultPrevented) return;
        const target = e.target;
        if (target instanceof HTMLElement && (target.isContentEditable || target.matches('textarea, input:not([type=checkbox])'))) return;
        e.preventDefault();
        dotnet.invokeMethodAsync('OnCookKey', e.key);
    };
    document.addEventListener('keydown', keyHandler);
}

export function stopListeningForKeys() {
    if (keyHandler) document.removeEventListener('keydown', keyHandler);
    keyHandler = null;
}

let audio = null;

// Browsers only let a page play sound after the user has interacted with it. Called when a timer
// starts, which is a tap, so the sound is allowed when the timer later ends.
export function prepareSound() {
    const Context = window.AudioContext || window.webkitAudioContext;
    if (!Context) return;
    audio ??= new Context();
    if (audio.state === 'suspended') audio.resume();
}

// Five short beeps.
export function ring() {
    prepareSound();
    if (!audio) return;

    const start = audio.currentTime + 0.05;
    for (let i = 0; i < 5; i++) {
        const at = start + i * 0.35;
        const tone = audio.createOscillator();
        const gain = audio.createGain();
        tone.type = 'sine';
        tone.frequency.value = 880;
        gain.gain.setValueAtTime(0.0001, at);
        gain.gain.exponentialRampToValueAtTime(0.4, at + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.2);
        tone.connect(gain).connect(audio.destination);
        tone.start(at);
        tone.stop(at + 0.22);
    }
}
