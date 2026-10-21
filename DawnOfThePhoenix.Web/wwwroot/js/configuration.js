// The server's /config.js endpoint provides public values only, never Stripe secrets.
export const configuration = window.PHOENIX_CONFIG;

export function httpsUrl(value) {
    try {
        const url = new URL(value);
        return url.protocol === 'https:' ? url.href : null;
    } catch {
        return null;
    }
}

export function bookingUrl(kind) {
    const url =
        kind === 'thirty'
            ? configuration.thirtyMinuteUrl || configuration.bookingUrl
            : kind === 'hour'
              ? configuration.sixtyMinuteUrl || configuration.bookingUrl
              : configuration.bookingUrl;

    return httpsUrl(url);
}

export function emailLink(subject, body = '') {
    return (
        'mailto:' +
        configuration.email +
        '?subject=' +
        encodeURIComponent(subject) +
        '&body=' +
        encodeURIComponent(body)
    );
}
