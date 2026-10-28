import { bookingUrl, emailLink } from './configuration.js';
import { showDialog } from './dialog.js';
import { mountCalendar } from './cal-embed.js';

export function initializeBooking() {
    document.querySelectorAll('[data-book]').forEach((button) => {
        button.addEventListener('click', () => openBooking(button.dataset.book));
    });
}

function openBooking(kind) {
    if (kind === 'choose') {
        showDialog(`
            <p class="eyebrow">LET’S CONNECT</p>
            <h2>Choose your session.</h2>
            <div class="modal-options">
                <button class="button" id="book-thirty" type="button">30-minute session · $30</button>
                <button class="button" id="book-hour" type="button">60-minute session · $55</button>
            </div>
        `);
        document.querySelector('#book-thirty').addEventListener('click', () => openBooking('thirty'));
        document.querySelector('#book-hour').addEventListener('click', () => openBooking('hour'));
        return;
    }
    const link = bookingUrl(kind);
    if (link) {
        showCalendar(link);
        return;
    }

    showEmailBooking(kind);
}

function showCalendar(link) {
    showDialog(`
        <p class="eyebrow">YOUR MOMENT STARTS HERE</p>
        <h2>Book time with Dawn.</h2>
        <p id="calendar-status" role="status">Loading Dawn’s calendar…</p>
        <div id="calendar"></div>
        <p class="modal-note">
            If the calendar doesn’t load,
            <a class="modal-email" id="external-calendar" target="_blank" rel="noopener noreferrer">
                open the booking page ↗
            </a>
        </p>
    `);

    document.querySelector('#external-calendar').href = link;
    mountCalendar(link, document.querySelector('#calendar'), document.querySelector('#calendar-status'));
}

function showEmailBooking(kind) {
    const heading =
        kind === 'thirty'
            ? '30 minutes of clarity.'
            : kind === 'hour'
              ? 'An hour to explore.'
              : 'Make a little space for you.';

    showDialog(`
        <p class="eyebrow">LET’S CONNECT</p>
        <h2>${heading}</h2>
        <p>
            Online scheduling is being set up. In the meantime, email Dawn to arrange
            your session and payment. Your time is confirmed directly with Dawn.
        </p>
        <div class="modal-options" id="booking-options"></div>
        <p class="modal-note">This email request does not reserve a time or collect payment.</p>
    `);

    const options =
        kind === 'choose'
            ? [
                  ['thirty', '30-minute session · $30'],
                  ['hour', '60-minute session · $55'],
              ]
            : [[kind, kind === 'thirty' ? 'Request 30 minutes · $30' : 'Request 60 minutes · $55']];

    for (const [key, label] of options) {
        const minutes = key === 'thirty' ? '30' : '60';
        const price = key === 'thirty' ? '30' : '55';
        const subject = `PhoenixRising: ${minutes}-minute session ($${price})`;
        const body =
            `Hi Dawn,\n\nI would like to arrange a ${minutes}-minute live session.\n\n` +
            'My preferred days/times and time zone are:\n\n' +
            'Please let me know your availability and how to pay.\n\nThank you!';

        const link = document.createElement('a');
        link.className = 'button';
        link.href = emailLink(subject, body);
        link.textContent = label + ' ↗';
        document.querySelector('#booking-options').append(link);
    }
}
