// Optional browser-module checks: node tests/browser/booking.mjs
// No npm dependencies or application build step. The DOM double checks booking actions only.
import assert from 'node:assert/strict';

class Element {
    constructor() { this.children = []; this.listeners = {}; this.dataset = {}; this.isConnected = true; }
    addEventListener(event, callback) { this.listeners[event] = callback; }
    click() { this.listeners.click(); }
    append(element) { this.children.push(element); }
    querySelector(tag) { return this.children.find(child => child.tag === tag); }
    set innerHTML(value) {
        this.html = value;
        for (const [selector, element] of elements) {
            if (selector !== '#modal' && selector !== '#modal-content') element.isConnected = false;
        }
        for (const id of value.matchAll(/id="([^"]+)"/g)) elements.set('#' + id[1], new Element());
    }
}
const elements = new Map([['#modal', new Element()], ['#modal-content', new Element()]]);
elements.get('#modal').showModal = () => {};
const buttons = ['choose', 'thirty', 'hour'].map(kind => Object.assign(new Element(), { dataset: { book: kind } }));
const scripts = [];
const sdkCalls = [];
globalThis.document = {
    head: { append: script => { scripts.push(script); } },
    querySelector: selector => elements.get(selector),
    querySelectorAll: selector => { assert.equal(selector, '[data-book]'); return buttons; },
    createElement: tag => Object.assign(new Element(), { tag })
};
globalThis.window = { PHOENIX_CONFIG: {
    bookingUrl: 'https://cal.com/dawn', thirtyMinuteUrl: 'https://cal.com/dawn/30-minute-reading',
    sixtyMinuteUrl: 'https://cal.com/dawn/60-minute-reading', email: 'dawn@example.com'
} };
const { initializeBooking } = await import('../../DawnOfThePhoenix.Web/wwwroot/js/booking.js');
initializeBooking();
buttons[0].click();
assert.ok(elements.has('#book-thirty') && elements.has('#book-hour'), 'Choose must offer the two priced sessions');
assert.equal(elements.has('#calendar'), false, 'Choose must not embed the generic profile');
elements.get('#book-thirty').click();
const firstCalendar = elements.get('#calendar');
assert.equal(scripts.length, 1);
assert.equal(scripts[0].src, 'https://app.cal.com/embed/embed.js');
assert.equal(elements.get('#external-calendar').href, 'https://cal.com/dawn/30-minute-reading');
// Switching sessions during script loading must embed only the current event.
buttons[2].click();
assert.equal(scripts.length, 1, 'Repeated opens must share one script load');
sdkCalls.push(...window.Cal.q);
window.Cal = (...args) => {
    sdkCalls.push(args);
    if (args[0] === 'inline') args[1].elementOrSelector.append(Object.assign(new Element(), { tag: 'iframe' }));
};
scripts[0].onload();
await new Promise(resolve => setImmediate(resolve));
assert.equal(sdkCalls.filter(([method]) => method === 'inline').length, 1);
assert.equal(firstCalendar.isConnected, false);
let inline = sdkCalls.find(([method]) => method === 'inline')[1];
assert.equal(inline.calLink, 'dawn/60-minute-reading');
assert.equal(inline.elementOrSelector.querySelector('iframe').allow, 'payment');
assert.equal(inline.elementOrSelector.querySelector('iframe').title, 'Schedule a session with Dawn');
assert.equal(inline.config.theme, 'dark');
assert.equal(inline.config.layout, 'month_view');
const appearance = sdkCalls.find(([method]) => method === 'ui')[1];
assert.equal(appearance.cssVarsPerTheme.dark['cal-brand'], '#e3a771');
assert.equal(appearance.hideEventTypeDetails, false);
buttons[1].click();
await new Promise(resolve => setImmediate(resolve));
inline = sdkCalls.filter(([method]) => method === 'inline').at(-1)[1];
assert.equal(inline.calLink, 'dawn/30-minute-reading');
assert.equal(scripts.length, 1);
window.PHOENIX_CONFIG.thirtyMinuteUrl = '';
buttons[1].click();
assert.ok(elements.get('#modal-content').html.includes('Online scheduling and payment are being set up'));
assert.equal(elements.get('#calendar').isConnected, false, 'Withheld event must not leave an active calendar');
assert.equal(elements.get('#external-calendar').isConnected, false, 'Withheld event must not leave an external booking link');
assert.equal(sdkCalls.filter(([method]) => method === 'inline').length, 2, 'Withheld event must not mount another calendar');
const email = elements.get('#booking-options').children[0];
assert.ok(email.href.startsWith('mailto:dawn@example.com?subject='));
assert.ok(decodeURIComponent(email.href).includes('30-minute live session'));
window.PHOENIX_CONFIG.sixtyMinuteUrl = '';
buttons[2].click();
assert.equal(elements.get('#calendar').isConnected, false, 'An unverified hour must not fall back to the configured profile');
assert.equal(elements.get('#external-calendar').isConnected, false, 'An unverified hour must not expose an external booking link');
assert.ok(decodeURIComponent(elements.get('#booking-options').children[0].href).includes('60-minute live session'));
window.PHOENIX_CONFIG.sixtyMinuteUrl = 'javascript:alert(1)';
buttons[2].click();
assert.ok(elements.get('#modal-content').html.includes('Online scheduling and payment are being set up'), 'Unsafe URL must use email fallback');
// A separate loader instance simulates blocked third-party scripts without network calls.
const { mountCalendar } = await import('../../DawnOfThePhoenix.Web/wwwroot/js/cal-embed.js?failure');
const status = new Element();
const pending = mountCalendar('https://cal.com/dawn/30-minute-reading', new Element(), status);
assert.equal(scripts.length, 2);
scripts[1].onerror();
await pending;
assert.ok(status.textContent.includes('could not load'));
await mountCalendar('https://untrusted.example/dawn/event', new Element(), status);
assert.equal(scripts.length, 2, 'Untrusted event URL must not load another script');
assert.ok(status.textContent.includes('booking page link'));
console.log('PASS: session selection, official embed, dark/copper appearance, payment permission, one-time script loading, stale-session protection, and fallbacks.');
