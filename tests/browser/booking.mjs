// Optional browser-module checks: node tests/browser/booking.mjs
// No npm dependencies or application build step. The DOM double checks booking actions only.
import assert from 'node:assert/strict';

class Element {
    constructor() { this.children = []; this.listeners = {}; this.dataset = {}; }
    addEventListener(event, callback) { this.listeners[event] = callback; }
    click() { this.listeners.click(); }
    append(element) { this.children.push(element); }
    set innerHTML(value) {
        this.html = value;
        for (const id of value.matchAll(/id="([^"]+)"/g)) elements.set('#' + id[1], new Element());
    }
}
const elements = new Map([['#modal', new Element()], ['#modal-content', new Element()]]);
elements.get('#modal').showModal = () => {};
const buttons = ['choose', 'thirty', 'hour'].map(kind => Object.assign(new Element(), { dataset: { book: kind } }));
globalThis.document = {
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
let frame = elements.get('#calendar').children[0];
assert.equal(frame.src, 'https://cal.com/dawn/30-minute-reading?embed=true');
assert.equal(frame.allow, 'payment');
assert.equal(elements.get('#external-calendar').href, 'https://cal.com/dawn/30-minute-reading');
buttons[2].click();
frame = elements.get('#calendar').children[0];
assert.equal(frame.src, 'https://cal.com/dawn/60-minute-reading?embed=true');
window.PHOENIX_CONFIG.thirtyMinuteUrl = '';
buttons[1].click();
assert.ok(elements.get('#modal-content').html.includes('Online scheduling is being set up'));
const email = elements.get('#booking-options').children[0];
assert.ok(email.href.startsWith('mailto:dawn@example.com?subject='));
assert.ok(decodeURIComponent(email.href).includes('30-minute live session'));
window.PHOENIX_CONFIG.sixtyMinuteUrl = 'javascript:alert(1)';
buttons[2].click();
assert.ok(elements.get('#modal-content').html.includes('Online scheduling is being set up'), 'Unsafe URL must use email fallback');
console.log('PASS: session chooser, correct event embeds, payment permission, external link, and missing/unsafe event fallback.');
