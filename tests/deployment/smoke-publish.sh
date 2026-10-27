#!/usr/bin/env bash
set -euo pipefail

publish_dir="$(cd "${1:?Usage: bash tests/deployment/smoke-publish.sh PUBLISH_DIRECTORY}" && pwd)"
scratch_dir="$(mktemp -d)"
app_pid=''
cleanup() {
    if [[ -n "$app_pid" ]]; then
        kill "$app_pid" 2>/dev/null || true
        wait "$app_pid" 2>/dev/null || true
    fi
    rm -rf "$scratch_dir"
}
trap cleanup EXIT

# Loopback and an OS-assigned port avoid exposing the test app or port collisions.
(
    cd "$publish_dir"
    ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production \
        ASPNETCORE_URLS=http://127.0.0.1:0 \
        Site__PublicBaseUrl=https://deployment-test.example \
        Stripe__SecretKey="${SMOKE_STRIPE_SECRET-smoke-test-secret-never-used}" \
        dotnet DawnOfThePhoenix.Web.dll
) > "$scratch_dir/app.log" 2>&1 &
app_pid=$!

base_url=''
for ((attempt = 0; attempt < 100; attempt++)); do
    base_url="$(sed -n 's/.*Now listening on: \(http:\/\/127\.0\.0\.1:[0-9]*\).*/\1/p' "$scratch_dir/app.log" | head -n 1)"
    if [[ -n "$base_url" ]]; then
        break
    fi
    if ! kill -0 "$app_pid" 2>/dev/null; then
        cat "$scratch_dir/app.log"
        exit 1
    fi
    sleep 0.1
done
if [[ -z "$base_url" ]]; then
    cat "$scratch_dir/app.log"
    echo 'FAIL: published application did not start.' >&2
    exit 1
fi

assert_response() {
    local route="$1" expected_type="$2" expected_text="$3"
    curl --silent --show-error --fail --max-time 10 \
        -D "$scratch_dir/headers" "$base_url$route" -o "$scratch_dir/body"
    grep -iq "^content-type: $expected_type" "$scratch_dir/headers"
    grep -Fq "$expected_text" "$scratch_dir/body"
}

assert_response / text/html PhoenixRising
grep -Fq request-verification-token "$scratch_dir/body"
grep -Fq 'Leave Dawn a tip' "$scratch_dir/body"
grep -Fq 'Tips are optional and don’t purchase a reading or reserve a session.' "$scratch_dir/body"
grep -Fq 'href="https://cash.app/$22PhoenixRising888"' "$scratch_dir/body"
grep -Fq 'href="https://www.paypal.com/cgi-bin/webscr?cmd=_xclick&amp;business=dawn%40dawnofthephoenix.com&amp;item_name=Tip%20for%20Dawn&amp;currency_code=USD&amp;no_shipping=1"' "$scratch_dir/body"
# No guessed Venmo destination or preset amount, including on the provider links.
if grep -Eiq 'venmo\.com|cash\.app/[^" ]+/[0-9]|[?&](amp;)?amount=' "$scratch_dir/body"; then
    echo 'FAIL: the tip jar contains an unconfigured provider or preset amount.' >&2
    exit 1
fi
if [[ -n "${SMOKE_STRIPE_SECRET-smoke-test-secret-never-used}" ]]; then
    grep -Fq 'id="tip"' "$scratch_dir/body"
elif grep -Fq 'id="tip"' "$scratch_dir/body"; then
    echo 'FAIL: card tipping must be hidden when Stripe is not configured.' >&2
    exit 1
fi
# Both QR images must appear on the page and survive publishing/versioning.
for method in cash-app paypal; do
    qr_url="$(grep -Eo "src=\"/assets/tips/$method(\.[a-z0-9]+)?\.svg(\?v=[^\"]+)?\"" "$scratch_dir/body" | cut -d '"' -f 2)"
    [[ -n "$qr_url" ]]
    curl --silent --show-error --fail --max-time 10 \
        -D "$scratch_dir/qr-headers" "$base_url$qr_url" -o "$scratch_dir/qr.svg"
    grep -iq '^content-type: image/svg+xml' "$scratch_dir/qr-headers"
    cmp "$publish_dir/wwwroot/assets/tips/$method.svg" "$scratch_dir/qr.svg"
done
# Keep the home-page response for the favicon checks below.
# Bookmark managers may use the root ICO independently of the tab's SVG.
# Versioned links also let browsers notice a replacement after deployment.
ico_link="$(grep -E '<link[^>]*href="/favicon(\.[a-z0-9]+\.ico|\.ico\?v=[^"]+)"' "$scratch_dir/body")"
grep -Fq 'rel="icon"' <<< "$ico_link"
grep -Fq 'type="image/x-icon"' <<< "$ico_link"
grep -Fq 'sizes="16x16 32x32 48x48 64x64"' <<< "$ico_link"
svg_link="$(grep -E '<link[^>]*href="/assets/favicon(\.[a-z0-9]+\.svg|\.svg\?v=[^"]+)"' "$scratch_dir/body")"
grep -Fq 'rel="icon"' <<< "$svg_link"
grep -Fq 'type="image/svg+xml"' <<< "$svg_link"
grep -Fq 'sizes="any"' <<< "$svg_link"
curl --silent --show-error --fail --max-time 10 \
    -D "$scratch_dir/headers" "$base_url/favicon.ico" -o "$scratch_dir/favicon.ico"
grep -Eiq '^content-type: image/(x-icon|vnd.microsoft.icon)' "$scratch_dir/headers"
cmp "$publish_dir/wwwroot/favicon.ico" "$scratch_dir/favicon.ico"
ico_url="$(sed -n 's/.*href="\([^"]*\)".*/\1/p' <<< "$ico_link")"
curl --silent --show-error --fail --max-time 10 "$base_url$ico_url" -o "$scratch_dir/linked-favicon.ico"
cmp "$scratch_dir/favicon.ico" "$scratch_dir/linked-favicon.ico"
# ICO header: reserved=0, type=icon, four frames. Prevent HTML/error fallbacks.
[[ "$(od -An -tx1 -N6 "$scratch_dir/favicon.ico" | tr -d ' \n')" == 000001000400 ]]
assert_response /terms text/html '<html'
assert_response /privacy text/html '<html'
assert_response /css/site.css text/css ':root'
assert_response /js/site.js text/javascript import
assert_response /assets/favicon.svg image/svg+xml '<svg'
assert_response /config.js application/javascript 'window.PHOENIX_CONFIG'
if grep -Fq smoke-test-secret-never-used "$scratch_dir/body"; then
    echo 'FAIL: public configuration exposed a server secret.' >&2
    exit 1
fi

# No antiforgery cookie/token: rejection must happen before any Stripe request.
status="$(curl --silent --show-error --max-time 10 -o "$scratch_dir/checkout" \
    -w '%{http_code}' -H 'Content-Type: application/json' \
    -d '{"kind":"questions"}' "$base_url/api/checkout")"
if [[ "$status" != 400 ]]; then
    echo "FAIL: checkout without an antiforgery token returned $status instead of 400." >&2
    exit 1
fi
echo 'PASS: published pages, assets, private configuration, and checkout antiforgery protection.'
