# Security boundaries

This is a preview built on WebView2, not a security-audited browser engine. Do not use its feature list as proof that it is safer than established browsers.

## Implemented boundaries

Untrusted web pages run in WebView2's normal sandbox, without host objects or web-message handlers. The app does not disable sandboxing, same-origin enforcement, certificate validation or reputation checking. Top-level and frame navigations are checked; only HTTPS is accepted by default, with an explicit HTTP setting. Privileged address-bar schemes and external-application launches are rejected. Popup requests must be user-initiated and HTTP(S). Certificate errors are canceled. Site permissions are denied without persistence. No downloads are auto-opened. The optional anti-redirect toggle blocks unsolicited cross-origin top-level page changes, while allowing explicit browser actions, clicked links, same-origin navigation and up to 20 server redirect hops in an authorized chain. Explicit Site files selections permit one client-side cross-origin handoff for 10 seconds. It can still interrupt script-driven cross-site login flows. It is not a guarantee against deceptive clicked links or same-origin navigation.

The Site files panel treats page metadata as untrusted, limits scan size to 10,000 elements and 100 results, polls only while open, validates protocols again in native code and sanitizes suggested filenames. Native save dialogs and runtime reputation checking still apply. Page authors can omit links, mislabel files or host malicious payloads. A list of detected files is not an endorsement. It does not crawl other pages or inspect server directories.

The blocker archive is pinned and hash checked, extracted afresh, and enabled before browsing. When blocking is enabled, failure prevents navigation. Users can disable uBOL in Privacy settings; strict runtime tracking prevention remains enabled. The upstream extension is unmodified. Local malware running as the same user can replace the executable, settings or runtime; hashes inside the executable do not protect against that attacker.

## Known limitations

- No independent audit, penetration test, anti-fingerprinting guarantees, bundled anonymity network/VPN service, password manager, full extension manager or equivalent Brave/LibreWolf hardening. No custom TLS stack.
- uBOL uses its upstream default declarative network filtering. No claim of full uBO feature parity or identical cosmetic filtering. Filter rules age until a new application release is installed.
- Microsoft updates WebView2 independently. N0VA's self-contained .NET runtime and app must be patched through new installers. No signed automatic app updates yet.
- SmartScreen and runtime services can contact Microsoft. WebRTC non-proxied UDP is restricted, not a claim that every network leak or fingerprint is prevented.
- Temporary profiles are ordinary disk files. Cleanup is best-effort after shutdown and retried at startup; it is not secure deletion, crash-proof confidentiality, or protection against local administrators. Downloads, preferences, favorites, download history and a previous saved profile are retained.
- HTTP opt-in is global. Subresource transport/mixed-content handling remains the Chromium engine's responsibility. Denying site permissions can break calls, device access, notifications, downloads using special APIs, and location features.
- Some websites reject embedded WebView2 sign-in; DRM, passkeys, extensions, downloads through blob URLs and other browser integrations may not work as in Edge/Brave/Firefox.
- Installer/app signing, accessible screen-reader testing, high-DPI testing on physical devices, Windows ARM64 builds and broad compatibility/performance benchmarking are not complete.

Report a vulnerability privately to the repository maintainer when a private channel is available. Avoid putting credentials or exploitable private details in a public issue.

## Connection and discovery limits

Proxy configuration covers the WebView2 browser route and the GitHub release/update metadata clients. It is not a device-wide tunnel or a guarantee against every Microsoft/runtime/OS network service bypass. Presets require the corresponding external service to be running. Invalid endpoints are rejected before constructing browser command-line arguments; proxy credentials, paths, argument separators and bypass lists are not accepted from user input. Fixed proxy configuration disables implicit localhost bypass and does not add a direct fallback. Local integration tests exercise both routing and failure behavior, not a real provider account or every VPN configuration.

The GitHub release resolver is activated by the user. It only contacts a validated GitHub API repository endpoint, refuses redirects, limits response size/time and validates every returned asset URL. It does not execute downloads, crawl arbitrary sites or make speculative requests to every link. GitHub can rate-limit requests. Counter values are observed network block events, not comprehensive ad/tracker coverage or unique-ad counts.


Startup update checks can be disabled in About. They query public GitHub releases through the configured route, without cookies or credentials, with redirects disabled, an 8-second timeout and a 2 MB limit. Only newer versioned x64 setup assets under N0VALID/N0VA are offered. The user may decline; accepting invokes the normal executable warning and save picker. The app never executes or silently installs an update. Repository/maintainer compromise remains a risk; signed manifests and signed installers are not implemented.


Connection tests contact ipify through the selected explicit proxy (no direct fallback, cookies, credentials or redirects), with an 8-second timeout and 4 KB response cap. A successful IP response is required before saving an enabled route; it is not proof of VPN anonymity. Disabling the proxy is a separate user action and still requires restart. No Tor process or VPN subscription is bundled.

Favorite icons come from the visited page's WebView2 favicon API. N0VA limits the PNG stream to 64 KB and the decoded dimensions to 512 × 512, re-encodes a 24 × 24 local image, and persists it only for bookmarked origins. No third-party icon service is queried. These icons persist alongside favorites in temporary mode.

Download-button discovery does not execute page event handlers or click arbitrary buttons. When no HTTP(S) target is available, the chosen button is scrolled into view, focused and briefly outlined for the user. The Epic launcher suggestion is restricted to exact official HTTPS hostnames and uses Epic's public installer endpoint. It does not imply that arbitrary page files are safe or that every site's generated downloads can be enumerated.


Native WebView2 download UI is retained so reputation prompts and blocked-download reasons are visible. N0VA never equates byte-count equality with successful completion, never forces a pending file into Completed, and does not disable SmartScreen or automatically accept a warning. Only the runtime Completed state enables the saved-file action. A periodic state reconciliation complements events. Download tests save known test files and the project's own published installer; they do not execute downloads or bypass security decisions.
