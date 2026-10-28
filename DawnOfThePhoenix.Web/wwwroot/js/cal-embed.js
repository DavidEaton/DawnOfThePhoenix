// Cal.com's official embed runs inside our existing click-triggered dialog.
// Load it only when a visitor opens a configured session; no bundler is needed.
let embedReady;

function loadEmbed() {
    if (embedReady) return embedReady;
    embedReady = new Promise((resolve, reject) => {
        // Cal.com's SDK consumes this instruction queue when its script loads.
        const cal = (...args) => cal.q.push(args);
        cal.q = [];
        cal.ns = {};
        window.Cal = cal;
        cal('init', { origin: 'https://cal.com' });
        const script = document.createElement('script');
        const timeout = setTimeout(() => reject(new Error('Calendar unavailable')), 12000);
        script.onload = () => {
            clearTimeout(timeout);
            resolve(window.Cal);
        };
        script.onerror = () => {
            clearTimeout(timeout);
            reject(new Error('Calendar unavailable'));
        };
        script.async = true;
        script.src = 'https://app.cal.com/embed/embed.js';
        document.head.append(script);
    });
    return embedReady;
}

export async function mountCalendar(link, container, status) {
    const url = new URL(link);
    // Only embed the public Cal.com event paths supplied by our server.
    if (url.origin !== 'https://cal.com' || !/^\/[a-zA-Z0-9_-]+\/[a-zA-Z0-9_-]+$/.test(url.pathname)) {
        status.textContent = 'Please use the booking page link below.';
        return;
    }
    try {
        const cal = await loadEmbed();
        // Switching sessions while the script loads must not mount a stale event.
        if (!container.isConnected) return;
        cal('inline', {
            elementOrSelector: container,
            calLink: url.pathname.slice(1),
            config: { theme: 'dark', layout: 'month_view' },
        });
        const frame = container.querySelector('iframe');
        if (frame) {
            frame.title = 'Schedule a session with Dawn';
            frame.allow = 'payment';
        }
        cal('ui', {
            theme: 'dark', layout: 'month_view', hideEventTypeDetails: false,
            cssVarsPerTheme: { dark: { 'cal-brand': '#e3a771' } },
        });
        status.textContent = 'Choose a time in your time zone and complete payment to book.';
    } catch {
        if (container.isConnected) status.textContent = 'The calendar could not load. Please open the booking page below.';
    }
}
