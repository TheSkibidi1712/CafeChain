/**
 * Shared Admin AJAX helpers (#129) — antiforgery + JSON POST only.
 * No business rules; server remains authority.
 */
(function (window, $) {
    'use strict';

    function getAntiforgeryToken() {
        if (!$) return '';
        return $('input[name="__RequestVerificationToken"]').val() || '';
    }

    function antiforgeryHeaders(extra) {
        var headers = {
            'RequestVerificationToken': getAntiforgeryToken(),
            'Content-Type': 'application/json'
        };
        if (extra) {
            Object.keys(extra).forEach(function (k) { headers[k] = extra[k]; });
        }
        return headers;
    }

    function postJson(url, body, options) {
        options = options || {};
        return $.ajax({
            url: url,
            method: 'POST',
            headers: antiforgeryHeaders(options.headers),
            data: typeof body === 'string' ? body : JSON.stringify(body || {}),
            contentType: 'application/json'
        });
    }

    window.CafeChainAdminAjax = {
        getAntiforgeryToken: getAntiforgeryToken,
        antiforgeryHeaders: antiforgeryHeaders,
        postJson: postJson
    };
})(window, window.jQuery);
