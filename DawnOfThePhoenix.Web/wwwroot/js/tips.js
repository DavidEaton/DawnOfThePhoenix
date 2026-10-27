import { startCheckout } from './checkout.js';
import { showDialog } from './dialog.js';

export function initializeTips() {
    document.querySelector('#tip')?.addEventListener('click', showTipCheckout);
}

function showTipCheckout() {
    showDialog(`
        <p class="eyebrow">A LITTLE LOVE</p>
        <h2>Leave Dawn a tip</h2>
        <p>Tips are optional and don’t purchase a reading or reserve a session.</p>
        <form id="tip-form">
            <label>
                Tip amount (USD)
                <input id="tip-amount" type="number" min="1" max="1000" step="1" placeholder="Choose an amount" required>
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
