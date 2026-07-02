/// <summary>
/// Fix Facebook Messenger layout in a WebView2 wrapper
/// </summary>
(function () {

    /// <summary>
    /// Inject CSS to hide the navigation bar and fix layout issues caused by Facebook's inline styles
    /// </summary>
    const injectStyle = () => {
        if (document.getElementById('messenger-wrapper-style')) return;

        const style = document.createElement('style');
        style.id = 'messenger-wrapper-style';
        style.textContent = `

            /* ── Hide navigation bar ──────────────────────────────────── */
            header,
            [role="banner"] {
                display:        none !important;
                visibility:     hidden !important;
                pointer-events: none !important;
            }

            /* ── Zero 56px gap ────────────────────────────────────── */

            /* Facebook lägger top: 56px / top:56px som inline-style på den
               absolut-positionerade content-ramen som ska ligga precis under
               navigeringsfältet. När headern försvinner måste vi nollställa
               den, annars blir det precis ett 56px tomrum längst upp. */
            [style*="top: 56px"],
            [style*="top:56px"] {
                top: 0 !important;
            }

            /* Same thing for padding */
            [style*="padding-top: 56px"],
            [style*="padding-top:56px"] {
                padding-top: 0 !important;
            }

            [style*="margin-top: 56px"],
            [style*="margin-top:56px"] {
                margin-top: 0 !important;
            }

            /* Content-area is often at calc(100vh - 56px) height to fill the entire viewport below the header — we adjust to full height */
            [style*="height: calc(100vh - 56px)"],
            [style*="height:calc(100vh - 56px)"] {
                height: 100vh !important;
            }

            [style*="min-height: calc(100vh - 56px)"],
            [style*="min-height:calc(100vh - 56px)"] {
                min-height: 100vh !important;
            }

            /* ── Full height layout ─────────────────────────────────────────── */
            html, body {
                height:   100% !important;
                overflow: hidden !important;
            }
        `;
        (document.head || document.documentElement).appendChild(style);
    };


    /// <summary>
    /// Fix the main layout to fill the entire WebView2 wrapper
    /// </summary>
    const fixMainLayout = () => {
        const main = document.querySelector('[role="main"]');
        if (!main) return;

        main.style.setProperty('top', '0', 'important');
        main.style.setProperty('margin-top', '0', 'important');
        main.style.setProperty('padding-top', '0', 'important');
        main.style.setProperty('height', '100vh', 'important');
        main.style.setProperty('max-height', '100vh', 'important');

        // Facebook is nestling [role="main"] in many div layers — reset the
        // position uppwards in the tree so that no parent element will get an offset from the top of the WebView2 wrapper
        let node = main.parentElement;
        for (let i = 0; node && i < 4; i++, node = node.parentElement) {
            node.style.setProperty('top', '0', 'important');
            node.style.setProperty('margin-top', '0', 'important');
            node.style.setProperty('padding-top', '0', 'important');
        }
    };

    /// <summary>
    /// Remove any banners or headers that Facebook may insert
    /// </summary>
    const removeBanners = () => {
        document.querySelectorAll('header, [role="banner"]').forEach(el => el.remove());
    };


    /// <summary>
    /// Setup the script to run on page load and on DOM changes
    /// </summary>
    const setup = () => {
        if (!document.body) {
            setTimeout(setup, 50);
            return;
        }

        injectStyle();
        fixMainLayout();
        removeBanners();

        /// <summary>
        /// Observe DOM changes and re-apply layout fixes
        /// </summary>
        new MutationObserver(() => {
            fixMainLayout();
            removeBanners();
        }).observe(document.body, { childList: true, subtree: true });
    };

    setup();


    /// <summary>
    /// Intercept clicks on links to Facebook and send them to the host app
    /// </summary>
    document.addEventListener('click', e => {
        const link = e.target?.closest?.('a[href]');
        if (!link) return;
        try {
            const url = new URL(link.href, location.href);
            const isFB = url.hostname === 'facebook.com' ||
                url.hostname.endsWith('.facebook.com');
            const isMsg = url.pathname.toLowerCase().startsWith('/messages');
            const isAuth = url.pathname.toLowerCase().includes('login') ||
                url.pathname.toLowerCase().startsWith('/checkpoint') ||
                url.pathname.toLowerCase().startsWith('/two_factor') ||
                url.pathname.toLowerCase().startsWith('/two_step_verification') ||
                url.pathname.toLowerCase().startsWith('/approvals') ||
                url.pathname.toLowerCase().startsWith('/security/');
            if (isFB && !isMsg && !isAuth) {
                e.preventDefault();
                e.stopPropagation();
                window.chrome.webview.postMessage(JSON.stringify({ externalUrl: url.href }));
            }
        } catch (_) { }
    }, true);

})();