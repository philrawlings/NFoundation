/**
 * PhotinoWindow.js - A typed message handling wrapper for Photino.NET
 * Provides a clean API for handling messages and request-response patterns
 */

(function(window) {
    'use strict';

    // Main PhotinoWindow object
    const PhotinoWindow = {
        // Storage for handlers
        _messageHandlers: new Map(),
        _pendingRequests: new Map(),
        _requestIdCounter: 0,
        _initialized: false,
        _debug: false,

        /**
         * Initialize the message handling system
         * @param {Object} options - Configuration options
         * @param {boolean} options.debug - Enable debug logging
         */
        initialize(options = {}) {
            if (this._initialized) {
                console.warn('PhotinoWindow already initialized');
                return this;
            }

            this._debug = options.debug || false;

            // Set up the global message receiver
            window.external.receiveMessage((message) => {
                this._handleIncomingMessage(message);
            });

            this._initialized = true;
            this._log('PhotinoWindow initialized');
            return this;
        },

        /**
         * Register a one-way message handler
         * @param {string} type - The message type to handle
         * @param {Function} handler - The handler function (receives payload)
         */
        onMessage(type, handler) {
            if (typeof type !== 'string' || !type) {
                throw new Error('Message type must be a non-empty string');
            }
            if (typeof handler !== 'function') {
                throw new Error('Handler must be a function');
            }

            if (this._messageHandlers.has(type)) {
                console.warn(`Overwriting existing message handler for type: ${type}`);
            }

            this._messageHandlers.set(type, handler);
            this._log(`Registered message handler for type: ${type}`);
            return this;
        },


        /**
         * Remove a message handler
         * @param {string} type - The message type
         */
        offMessage(type) {
            const removed = this._messageHandlers.delete(type);
            if (removed) {
                this._log(`Removed message handler for type: ${type}`);
            }
            return this;
        },


        /**
         * Send a one-way message to C#
         * @param {string} type - The message type
         * @param {*} payload - The message payload
         */
        sendMessage(type, payload) {
            if (typeof type !== 'string' || !type) {
                throw new Error('Message type must be a non-empty string');
            }

            const message = {
                type: type,
                payload: payload || null
            };

            this._sendToHost(message);
            this._log(`Sent message of type: ${type}`, payload);
            return this;
        },

        /**
         * Send a request to C# and wait for response
         * @param {string} type - The request type
         * @param {*} payload - The request payload
         * @param {number} timeout - Optional timeout in milliseconds (default: 30000)
         * @returns {Promise} - Promise that resolves with the response
         */
        sendRequest(type, payload, timeout = 30000) {
            if (typeof type !== 'string' || !type) {
                throw new Error('Request type must be a non-empty string');
            }

            return new Promise((resolve, reject) => {
                const requestId = this._generateRequestId();

                // Set up timeout
                const timeoutHandle = setTimeout(() => {
                    this._pendingRequests.delete(requestId);
                    reject(new Error(`Request '${type}' timed out after ${timeout}ms`));
                }, timeout);

                // Store the pending request
                this._pendingRequests.set(requestId, {
                    resolve: (response) => {
                        clearTimeout(timeoutHandle);
                        resolve(response);
                    },
                    reject: (error) => {
                        clearTimeout(timeoutHandle);
                        reject(error);
                    },
                    type: type
                });

                // Send the request
                const request = {
                    type: type,
                    requestId: requestId,
                    payload: payload || null
                };

                this._sendToHost(request);
                this._log(`Sent request of type: ${type} with id: ${requestId}`, payload);
            });
        },

        /**
         * Handle incoming messages from C#
         * @private
         */
        _handleIncomingMessage(messageString) {
            try {
                const message = JSON.parse(messageString);
                this._log('Received message:', message);

                // Check if this is a response to a pending request
                if (message.type === 'response' && message.requestId) {
                    this._handleResponse(message);
                    return;
                }

                // Check if this is a request from C# (has requestId) - not supported
                if (message.requestId && message.type !== 'response') {
                    this._log(`Received unsupported request from C#: ${message.type}`);
                    return;
                }

                // Otherwise it's a one-way message
                this._handleIncomingOneWayMessage(message);

            } catch (error) {
                console.error('Error handling incoming message:', error, messageString);
            }
        },

        /**
         * Handle incoming one-way message
         * @private
         */
        _handleIncomingOneWayMessage(message) {
            const handler = this._messageHandlers.get(message.type);

            if (handler) {
                try {
                    handler(message.payload);
                    this._log(`Handled message of type: ${message.type}`);
                } catch (error) {
                    console.error(`Error in message handler for type '${message.type}':`, error);
                }
            } else {
                this._log(`No handler registered for message type: ${message.type}`);
            }
        },


        /**
         * Handle response from C#
         * @private
         */
        _handleResponse(message) {
            const pending = this._pendingRequests.get(message.requestId);

            if (pending) {
                this._pendingRequests.delete(message.requestId);

                if (message.error) {
                    pending.reject(new Error(message.error));
                } else {
                    pending.resolve(message.payload);
                }

                this._log(`Handled response for request: ${message.requestId}`);
            } else {
                console.warn(`Received response for unknown request: ${message.requestId}`);
            }
        },

        /**
         * Send message to host (C#)
         * @private
         */
        _sendToHost(message) {
            const messageString = JSON.stringify(message);
            window.external.sendMessage(messageString);
        },

        /**
         * Generate a unique request ID
         * @private
         */
        _generateRequestId() {
            return `req-${Date.now()}-${++this._requestIdCounter}`;
        },

        /**
         * Log debug messages
         * @private
         */
        _log(...args) {
            if (this._debug) {
                console.log('[PhotinoWindow]', ...args);
            }
        },

        /**
         * Clear all handlers
         */
        clearHandlers() {
            this._messageHandlers.clear();
            this._log('Cleared all handlers');
            return this;
        },

        /**
         * Get statistics about registered handlers
         */
        getStats() {
            return {
                messageHandlers: this._messageHandlers.size,
                pendingRequests: this._pendingRequests.size,
                initialized: this._initialized
            };
        }
    };

    // Helper function for common patterns
    PhotinoWindow.helpers = {};

    // Expose PhotinoWindow globally
    window.PhotinoWindow = PhotinoWindow;

})(window);