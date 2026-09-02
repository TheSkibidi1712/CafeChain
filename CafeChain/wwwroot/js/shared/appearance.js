(function () {
    'use strict';

    // Theme switching was removed. Keep every surface on the canonical light
    // palette and discard the obsolete preference so it cannot return on reload.
    document.documentElement.dataset.theme = 'light';
    document.documentElement.dataset.themePreference = 'light';
    document.documentElement.style.colorScheme = 'light';
    try { localStorage.removeItem('cafechain.theme'); } catch (_) { /* storage can be unavailable */ }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-culture-selector]').forEach(function (select) {
            select.value = document.documentElement.dataset.culture || 'vi-VN';
            select.addEventListener('change', function () {
                var returnUrl = window.location.pathname + window.location.search + window.location.hash;
                window.location.assign('/ui-preferences/culture?culture=' + encodeURIComponent(select.value)
                    + '&returnUrl=' + encodeURIComponent(returnUrl));
            });
        });
    });

}());
