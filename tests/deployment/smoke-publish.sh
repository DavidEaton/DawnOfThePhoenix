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
        Stripe__SecretKey=smoke-test-secret-never-used \
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
