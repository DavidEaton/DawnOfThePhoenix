import { configuration, emailLink, httpsUrl } from './configuration.js';
import { startCheckout } from './checkout.js';
import { showDialog } from './dialog.js';

export function initializeTips() {
    document.querySelector('#tip')?.addEventListener('click', () => {
        if (configuration.paymentsReady) {
            showTipCheckout();
            return;
        }

        const link = httpsUrl(configuration.tipUrl);
        if (link) {
            window.open(link, '_blank', 'noopener,noreferrer');
            return;
        }

        showEmailTip();
    });
}

function showTipCheckout() {
    showDialog(`
        <p class="eyebrow">A LITTLE LOVE</p>
        <h2>Leave a little gratitude.</h2>
        <p>Tips are optional and always appreciated.</p>
        <form id="tip-form">
            <label>
                Tip amount (USD)
                <input id="tip-amount" type="number" min="1" max="1000" step="1" value="10" required>
            </label>
            <button type="submit" class="button">Continue to secure checkout <span>↗</span></button>
            <p role="status" id="checkout-error"></p>
        </form>
    `);
    document.querySelector('#tip-form').addEventListener('submit', (event) => {
        event.preventDefault();
        const dollars = Number(document.querySelector('#tip-amount').value);
        startCheckout(
            { kind: 'tip', amount: Math.round(dollars * 100) },
            event.target.querySelector('button')
        );
    });
}

function showEmailTip() {
    showDialog(`
        <p class="eyebrow">A LITTLE LOVE</p>
        <h2>Thank you for your kindness.</h2>
        <p>
            Dawn’s online tip jar is being set up. If you’d like to leave a tip,
            contact Dawn for her preferred payment method.
        </p>
        <a class="button" id="tip-email">Ask Dawn about tipping <span>↗</span></a>
        <p class="modal-note">Tips are optional and always appreciated. No payment is collected here.</p>
    `);
    document.querySelector('#tip-email').href = emailLink(
        'A little gratitude for Dawn',
        'Hi Dawn,\n\nI would like to leave you a tip. What is your preferred payment method?\n\nThank you!'
    );
}
