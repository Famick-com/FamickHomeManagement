/**
 * Theme preference storage.
 *
 * Kept in localStorage rather than on the server because the sign-in screen needs it before
 * there is a session to read a profile from. Deliberately per-device: someone on a bright
 * desktop and a dark phone usually wants different answers.
 */
window.famickTheme = {
    KEY: 'theme_preference',

    /**
     * The stored choice, or null when the user has never expressed one — which the caller
     * needs to tell apart from "chose light", so that the OS is only consulted while no
     * explicit choice exists.
     * @returns {boolean|null}
     */
    getStored: function () {
        try {
            var value = localStorage.getItem(this.KEY);
            if (value === 'dark') return true;
            if (value === 'light') return false;
            return null;
        } catch (e) {
            return null;
        }
    },

    /** @param {boolean} isDark */
    store: function (isDark) {
        try {
            localStorage.setItem(this.KEY, isDark ? 'dark' : 'light');
        } catch (e) {
            // Private browsing, or storage disabled. The choice still applies for this
            // session; it just will not be remembered.
        }
    },

    /**
     * What the operating system asks for. Only consulted when nothing is stored.
     * @returns {boolean}
     */
    prefersDark: function () {
        try {
            return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        } catch (e) {
            return false;
        }
    },

    /**
     * Resolved preference: the explicit choice if there is one, otherwise the OS.
     * @returns {boolean}
     */
    resolve: function () {
        var stored = this.getStored();
        return stored === null ? this.prefersDark() : stored;
    },

    /**
     * Paints the page background before Blazor starts.
     *
     * The app is WebAssembly, so there is a visible gap between the browser rendering the
     * shell and the framework being able to theme anything. Without this a dark-mode user
     * gets a white flash on every load, which reads as the page breaking.
     */
    applyPreBoot: function () {
        try {
            var dark = this.resolve();
            var background = dark ? '#121212' : '#FFFFFF';
            document.documentElement.style.backgroundColor = background;
            if (document.body) document.body.style.backgroundColor = background;
        } catch (e) {
            // Leave the default background.
        }
    }
};

window.famickTheme.applyPreBoot();
