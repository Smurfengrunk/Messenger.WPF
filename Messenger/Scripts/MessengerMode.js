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

            /* ── Zero out the header-height CSS variable ─────────────────── */
            :root,
            .__fb-light-mode:root,
            .__fb-light-mode,
            .__fb-dark-mode {
                --header-height: 0px;
            }

            /* ── Hide navigation bar ──────────────────────────────────── */
            header,
            [role="banner"] {
                display:        none !important;
                visibility:     hidden !important;
                pointer-events: none !important;
            }

            /* Position adjustment so that both containers start at the same position vertically */
            [role="navigation"] {
                margin-top: 16px !important;
            }

            /* ── Zero 56px gap ────────────────────────────────────── */
            [style*="top: 56px"],
            [style*="top:56px"] {
                top: 0 !important;
                box-sizing: border-box !important;
            }

            [style*="padding-top: 56px"],
            [style*="padding-top:56px"] {
                padding-top: 0 !important;
                box-sizing: border-box !important;
            }

            [style*="margin-top: 56px"],
            [style*="margin-top:56px"] {
                margin-top: 0 !important;
                box-sizing: border-box !important;
            }

            [style*="height: calc(100vh - 56px)"],
            [style*="height:calc(100vh - 56px)"] {
                height: 100vh !important;
                box-sizing: border-box !important;
            }

            [style*="height: calc(100% - 56px)"],
            [style*="height:calc(100% - 56px)"] {
                height: 100% !important;
                box-sizing: border-box !important;
            }

            [style*="min-height: calc(100vh - 56px)"],
            [style*="min-height:calc(100vh - 56px)"] {
                min-height: 100vh !important;
                box-sizing: border-box !important;
            }

            [style*="min-height: calc(100% - 56px)"],
            [style*="min-height:calc(100% - 56px)"] {
                min-height: 100% !important;
                box-sizing: border-box !important;
            }

            /* ── Full height layout ─────────────────────────────────────────── */
            html, body {
                height:   100% !important;
                overflow: hidden !important;
            }

            /* overflow: hidden on the outer frame elements prevents WebView2
               from adding a page-level scrollbar. We intentionally exclude
               .__fb-light-mode and .__fb-dark-mode here — those classes are
               used on wrappers deep inside the panel tree, and setting
               overflow: hidden on them clips the chat list's own scrollbar. */
            html, body, [role="main"], #root {
                overflow: hidden !important;
            }

            /* Hide the page-level (document) scrollbar that WebView2 would
               show for any residual layout overflow. Targeting only html and
               body leaves all inner scrollbars (message list, chat list)
               completely unaffected. */
            html::-webkit-scrollbar,
            body::-webkit-scrollbar {
                display: none !important;
                width:   0 !important;
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

        // Force the main element and its parents to fill the entire viewport and prevent scrolling
        const setFixed = (el) => {
            el.style.setProperty('top', '0', 'important');
            el.style.setProperty('margin-top', '0', 'important');
            el.style.setProperty('padding-top', '0', 'important');
            el.style.setProperty('height', 'calc(100vh - 16px)', 'important');
            el.style.setProperty('max-height', 'calc(100vh - 16px)', 'important');
            el.style.setProperty('overflow', 'hidden', 'important'); // Key: prevent scrolling
        };

        setFixed(main);

        let node = main.parentElement;
        for (let i = 0; node && i < 4; i++, node = node.parentElement) {
            setFixed(node);
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