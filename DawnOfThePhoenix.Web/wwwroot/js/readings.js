import { configuration, emailLink } from './configuration.js';
import { startCheckout } from './checkout.js';
import { showDialog } from './dialog.js';

export function initializeReadings() {
    document.querySelector('#ask')?.addEventListener('click', () => {
        if (configuration.paymentsReady) {
            showReadingCheckout();
        } else {
            showEmailQuestions();
        }
    });
}

function showReadingCheckout() {
    showDialog(`
        <p class="eyebrow">THREE QUESTIONS · $8</p>
        <h2>A little clarity awaits.</h2>
        <p>
            On the secure Stripe checkout page, enter your three yes-or-no questions
            and the email where Dawn should send your reading. Dawn answers within 48 hours of payment.
        </p>
        <p>
            Please avoid sensitive medical, financial, or other private information.
            Readings are for entertainment purposes only.
        </p>
        <button class="button" id="pay-questions">
            Continue to secure checkout · $8 <span>↗</span>
        </button>
        <p role="status" id="checkout-error"></p>
    `);
    document.querySelector('#pay-questions').addEventListener('click', (event) => {
        startCheckout({ kind: 'questions' }, event.currentTarget);
    });
}

function showEmailQuestions() {
    const fields = [1, 2, 3]
        .map(
            (number) => `
        <label>
            Question ${number}
            <textarea name="q${number}" rows="2" maxlength="150" required
                placeholder="Your yes-or-no question"></textarea>
        </label>
    `
        )
        .join('');

    showDialog(`
        <p class="eyebrow">THREE QUESTIONS · $8</p>
        <h2>What’s on your heart?</h2>
        <p>
            Write three questions that can each be answered yes or no. Dawn answers within 48 hours.
            This form opens an email draft for you to send; arrange payment directly with Dawn.
        </p>
        <form id="question-form">
            <label>Your name<input name="name" autocomplete="name" maxlength="100" required></label>
            ${fields}
            <label>
                <input type="checkbox" required>
                I understand readings are for entertainment purposes only.
            </label>
            <button class="button" type="submit">Prepare email to Dawn <span>↗</span></button>
            <p class="modal-note" id="email-help">
                Nothing is sent or charged by this website. You’ll review and send the message in your email app.
            </p>
        </form>
    `);
    document.querySelector('#question-form').addEventListener('submit', prepareQuestionsEmail);
}

function prepareQuestionsEmail(event) {
    event.preventDefault();
    const data = new FormData(event.target);
    const questions = [1, 2, 3].map((number) => `${number}. ${data.get('q' + number)}`);
    const body =
        'Hi Dawn,\n\nMy name is ' +
        data.get('name') +
        '. I would like the three yes/no question reading for $8.\n\n' +
        questions.join('\n\n') +
        '\n\nPlease let me know how to pay and confirm when my reading will be ready.' +
        '\n\nI understand this is for entertainment purposes only.';

    window.location.href = emailLink('PhoenixRising: Three yes/no questions ($8)', body);
    document.querySelector('#email-help').textContent =
        'Email draft prepared. Please send it from your email app. If no app opened, email your questions to ' +
        configuration.email +
        '. Your questions have not been submitted through this website.';
}
