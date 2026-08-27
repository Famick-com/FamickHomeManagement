/**
 * Time zone detection.
 *
 * The server can only report its own zone, which on a hosted install is whatever the container
 * runs as — usually UTC, and rarely where the household actually is. The browser knows the real
 * answer, so ask it and offer that instead.
 */
window.famickTimeZone = {
    /**
     * The IANA zone name for this device, e.g. "America/New_York".
     * @returns {string|null} null when the browser cannot say, so callers keep their default.
     */
    get: function () {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
        } catch (e) {
            return null;
        }
    }
};
