import { replaceDialogContent, showDialog } from './dialog.js';

// Payment requests stay separate from the reading and tip presentation code.
export async function startCheckout(payload, button) {
    const status = document.querySelector('#checkout-error');
    button.disabled = true;
    status.textContent = 'Opening secure checkout…';

    try {
        const response = await fetch('/api/checkout', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                RequestVerificationToken: document.querySelector(
                    'meta[name="request-verification-token"]'
                ).content,
            },
            body: JSON.stringify(payload),
        });
        const data = await response.json();
        if (!response.ok) {
            throw new Error(data.error || 'Checkout could not be opened.');
        }

        const url = new URL(data.url);
        if (url.protocol !== 'https:' || url.hostname !== 'checkout.stripe.com') {
            throw new Error('Invalid checkout destination.');
        }

        window.location.assign(url.href);
    } catch (error) {
        status.textContent = error.message || 'Checkout is unavailable. Please try again.';
        button.disabled = false;
    }
}

export async function verifyCheckoutReturn() {
    const query = new URLSearchParams(location.search);
    if (query.get('checkout') === 'cancelled') {
        showDialog(`
            <h2>No payment completed.</h2>
            <p>Your checkout was cancelled. You can return to the readings whenever you’re ready.</p>
        `);
        clearCheckoutQuery();
        return;
    }

    if (query.get('checkout') !== 'complete') {
        return;
    }

    showDialog(`
        <h2>Checking your payment…</h2>
        <p role="status">Please wait while we confirm your checkout with Stripe.</p>
    `);

    try {
        const sessionId = encodeURIComponent(query.get('session_id') || '');
        const response = await fetch('/api/checkout-status?session_id=' + sessionId);
        const data = await response.json();
        if (!response.ok) {
            throw new Error('Payment verification failed.');
        }

        if (!data.paid) {
            replaceDialogContent(`
                <h2>Payment not yet confirmed.</h2>
                <p>Please check your Stripe receipt or contact Dawn before paying again.</p>
            `);
            return;
        }

        replaceDialogContent(
            data.kind === 'questions'
                ? `
            <h2>Thank you for connecting.</h2>
            <p>
                Your payment is confirmed. Dawn can view your three questions and email
                in Stripe and will respond within 48 hours. Keep your payment receipt for reference.
            </p>
        `
                : `
            <h2>Thank you for the love.</h2>
            <p>Your tip payment is confirmed. Your kindness is appreciated.</p>
        `
        );
    } catch {
        replaceDialogContent(`
            <h2>We couldn’t confirm that yet.</h2>
            <p>Please check your Stripe receipt or contact Dawn before paying again.</p>
        `);
    } finally {
        clearCheckoutQuery();
    }
}

function clearCheckoutQuery() {
    history.replaceState(null, '', location.pathname);
}
