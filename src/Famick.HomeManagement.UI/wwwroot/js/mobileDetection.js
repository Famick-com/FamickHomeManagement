/**
 * Mobile detection and deep linking utilities for Famick Shopping
 */
window.famickMobileDetection = {
    /**
     * Checks if the current device is a mobile device
     * @returns {boolean} True if mobile device
     */
    isMobile: function() {
        const userAgent = navigator.userAgent || navigator.vendor || window.opera;

        // Check for common mobile user agents
        const mobileRegex = /android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini|mobile|tablet/i;

        // Also check for touch support and screen size
        const isTouchDevice = 'ontouchstart' in window || navigator.maxTouchPoints > 0;
        const isSmallScreen = window.innerWidth <= 768;

        return mobileRegex.test(userAgent.toLowerCase()) || (isTouchDevice && isSmallScreen);
    },

    /**
     * Gets the mobile platform (iOS or Android)
     * @returns {string|null} "iOS", "Android", or null if not mobile
     */
    getPlatform: function() {
        const userAgent = navigator.userAgent || navigator.vendor || window.opera;
        const ua = userAgent.toLowerCase();

        if (/iphone|ipad|ipod/.test(ua)) {
            return "iOS";
        }

        if (/android/.test(ua)) {
            return "Android";
        }

        return null;
    },

    /**
     * Attempts to open the app via deep link with a timeout fallback
     * @param {string} deepLinkUrl - The deep link URL to attempt
     * @param {number} timeoutMs - Timeout in milliseconds (default 2000)
     * @returns {boolean} True if deep link was attempted
     */
    tryOpenApp: function(deepLinkUrl, timeoutMs) {
        timeoutMs = timeoutMs || 2000;

        return new Promise(function(resolve) {
            // Record the time before attempting to open the app
            const startTime = Date.now();
            let hasFocused = false;

            // Listen for visibility change (app opened and browser went to background)
            function onVisibilityChange() {
                if (document.hidden) {
                    hasFocused = true;
                }
            }

            document.addEventListener('visibilitychange', onVisibilityChange);

            // Attempt to open the deep link
            window.location.href = deepLinkUrl;

            // After timeout, check if we left the page
            setTimeout(function() {
                document.removeEventListener('visibilitychange', onVisibilityChange);

                // If the page went hidden (app opened), return true
                // Otherwise, the app likely isn't installed
                resolve(hasFocused);
            }, timeoutMs);
        });
    },

    /**
     * Checks if the app banner should be shown (not already dismissed in this session)
     * @returns {boolean} True if banner should be shown
     */
    shouldShowAppBanner: function() {
        // Only show on mobile devices
        if (!this.isMobile()) {
            return false;
        }

        // Check session storage for dismissal
        try {
            return sessionStorage.getItem('famick_app_banner_dismissed') !== 'true';
        } catch (e) {
            return true;
        }
    },

    /**
     * Dismisses the app banner for the current session
     */
    dismissAppBanner: function() {
        try {
            sessionStorage.setItem('famick_app_banner_dismissed', 'true');
        } catch (e) {
            // Session storage not available, ignore
        }
    }
};
