// WebAuthn/Passkey JavaScript interop for Blazor
window.passkeyAuth = {
    /**
     * Check if WebAuthn is supported by the browser
     * @returns {boolean} True if WebAuthn is available
     */
    isSupported: function() {
        return window.PublicKeyCredential !== undefined &&
               typeof window.PublicKeyCredential === 'function';
    },

    /**
     * Check if platform authenticator (e.g., Touch ID, Face ID, Windows Hello) is available
     * @returns {Promise<boolean>} True if platform authenticator is available
     */
    isPlatformAuthenticatorAvailable: async function() {
        if (!this.isSupported()) {
            return false;
        }
        try {
            return await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
        } catch {
            return false;
        }
    },

    /**
     * Register a new passkey credential
     * @param {string} optionsJson - JSON string of CredentialCreationOptions from server
     * @returns {Promise<string|null>} JSON string of attestation response or null if cancelled
     */
    register: async function(optionsJson) {
        if (!this.isSupported()) {
            throw new Error('WebAuthn is not supported in this browser');
        }

        // Parse the options from the server
        const options = JSON.parse(optionsJson);

        // Convert base64url strings to ArrayBuffers
        options.challenge = this._base64UrlToArrayBuffer(options.challenge);
        options.user.id = this._base64UrlToArrayBuffer(options.user.id);

        if (options.excludeCredentials) {
            options.excludeCredentials = options.excludeCredentials.map(cred => ({
                ...cred,
                id: this._base64UrlToArrayBuffer(cred.id)
            }));
        }

        try {
            // Call the WebAuthn API
            const credential = await navigator.credentials.create({
                publicKey: options
            });

            if (!credential) {
                return null;
            }

            // Format the response for the server
            const response = {
                id: credential.id,
                rawId: this._arrayBufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    clientDataJSON: this._arrayBufferToBase64Url(credential.response.clientDataJSON),
                    attestationObject: this._arrayBufferToBase64Url(credential.response.attestationObject)
                },
                clientExtensionResults: credential.getClientExtensionResults()
            };

            // Add transports if available
            if (credential.response.getTransports) {
                response.response.transports = credential.response.getTransports();
            }

            return JSON.stringify(response);
        } catch (error) {
            if (error.name === 'NotAllowedError') {
                // User cancelled or operation timed out
                return null;
            }
            throw error;
        }
    },

    /**
     * Authenticate with an existing passkey
     * @param {string} optionsJson - JSON string of CredentialRequestOptions from server
     * @returns {Promise<string|null>} JSON string of assertion response or null if cancelled
     */
    authenticate: async function(optionsJson) {
        if (!this.isSupported()) {
            throw new Error('WebAuthn is not supported in this browser');
        }

        // Parse the options from the server
        const options = JSON.parse(optionsJson);

        // Convert base64url strings to ArrayBuffers
        options.challenge = this._base64UrlToArrayBuffer(options.challenge);

        if (options.allowCredentials) {
            options.allowCredentials = options.allowCredentials.map(cred => ({
                ...cred,
                id: this._base64UrlToArrayBuffer(cred.id)
            }));
        }

        try {
            // Call the WebAuthn API
            const credential = await navigator.credentials.get({
                publicKey: options
            });

            if (!credential) {
                return null;
            }

            // Format the response for the server
            const response = {
                id: credential.id,
                rawId: this._arrayBufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    clientDataJSON: this._arrayBufferToBase64Url(credential.response.clientDataJSON),
                    authenticatorData: this._arrayBufferToBase64Url(credential.response.authenticatorData),
                    signature: this._arrayBufferToBase64Url(credential.response.signature),
                    userHandle: credential.response.userHandle ?
                        this._arrayBufferToBase64Url(credential.response.userHandle) : null
                },
                clientExtensionResults: credential.getClientExtensionResults()
            };

            return JSON.stringify(response);
        } catch (error) {
            if (error.name === 'NotAllowedError') {
                // User cancelled or operation timed out
                return null;
            }
            throw error;
        }
    },

    /**
     * Convert a base64url encoded string to an ArrayBuffer
     * @param {string} base64url - Base64url encoded string
     * @returns {ArrayBuffer} Decoded ArrayBuffer
     */
    _base64UrlToArrayBuffer: function(base64url) {
        // Convert base64url to base64
        let base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
        // Add padding if needed
        while (base64.length % 4) {
            base64 += '=';
        }
        // Decode
        const binaryString = atob(base64);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes.buffer;
    },

    /**
     * Convert an ArrayBuffer to a base64url encoded string
     * @param {ArrayBuffer} buffer - ArrayBuffer to encode
     * @returns {string} Base64url encoded string
     */
    _arrayBufferToBase64Url: function(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.length; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        // Convert to base64
        const base64 = btoa(binary);
        // Convert to base64url
        return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
    }
};
