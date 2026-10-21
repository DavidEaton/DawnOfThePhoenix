import { showDialog } from './dialog.js';

export function initializeLayout() {
    initializeNavigation();
    document.querySelector('#privacy').addEventListener('click', showPrivacy);
    document.querySelector('#year').textContent = new Date().getFullYear();
}

function initializeNavigation() {
    const menu = document.querySelector('.menu');
    const navigation = document.querySelector('nav');

    menu.addEventListener('click', () => {
        const open = menu.getAttribute('aria-expanded') !== 'true';
        menu.setAttribute('aria-expanded', String(open));
        navigation.classList.toggle('open', open);
    });

    navigation.querySelectorAll('a').forEach((link) => {
        link.addEventListener('click', () => {
            menu.setAttribute('aria-expanded', 'false');
            navigation.classList.remove('open');
        });
    });
}

function showPrivacy() {
    showDialog(`
        <p class="eyebrow">YOUR PRIVACY</p>
        <h2>A little peace of mind.</h2>
        <p>
            When online checkout is available, payments and written reading questions are processed by Stripe.
            Dawn receives the questions and contact information needed to fulfill your reading.
            Payment card details are handled by Stripe. If you use the email form instead, this page does not
            store the entries; they are shared with Dawn when you send the email. Please avoid including
            sensitive medical, financial, or other private details.
        </p>
        <p>
            This site uses an essential security cookie to protect checkout requests. It also uses Google Fonts,
            which may receive technical information such as your IP address when fonts load. Hosting services
            may process routine technical access logs. Cal.com handles scheduling and its associated booking
            information; Stripe handles checkout information. Those services have their own privacy policies.
            Contact Dawn with questions about information used to deliver your reading.
        </p>
        <p>
            To ask about information you have shared with Dawn, email
            <a class="modal-email" href="mailto:dawn@dawnofthephoenix.com">dawn@dawnofthephoenix.com</a>.
        </p>
    `);
}
