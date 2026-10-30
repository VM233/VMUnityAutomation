# Native Unity service checks

Discover the package-owned `unity/cloud-service-check` extension through the
official Unity CLI catalog. It returns a durable project-tool Job. Poll that
original Job and retain its actual async cleanup receipt, including on failure
or cancellation. Unity 6 provides the cancellable native API; earlier supported
Editors return `capability_unavailable` without starting a request.

The check invokes `CloudProjectSettings.GetServiceTokenAsync` once, with Unity's
own cache and a 15-second cancellation request. It does not force a refresh,
change accounts, clear tokens, unlink projects, alter proxy settings, or replay
a request after reload. `available` proves only that the API returned a nonempty
token in this lifetime. It does not prove a new network exchange, explain a
historical failure, or authorize calibration admission.

The immutable outcome contains no token, exception message, response body,
headers or stack. Failure metadata has a fixed allowlist: exception type, HTTP
status, network/HTTP flags, method, target host/path and native timeout. Userinfo,
query and fragment are removed from URLs; oversized URLs are not published.
Global Console handling remains unchanged.

One owned Work admits its actual retirement Task before invoking the API. Cancel,
Editor quit and assembly reload request cancellation of that same work. Only its
real `finally` releases the CTS and publishes the resource receipt. A delayed
native cancellation remains pending; it never starts another request or claims
resources released early. The source has at most 32 pending observation steps.

Static cost: one active Work, one native API invocation, one CTS/timer, one source
Task and one retirement Task; at most 32 O(1) pending steps. Metadata reads seven
fixed members, parses at most 2048 URL characters and publishes under 8 KiB for
the native exception domain. No asset/entity scan, gameplay tick, temporary
Unity object, logger subscription or new transport. This bounds owned work and
storage, not native scheduling or network latency. Native tests cover redaction,
availability versus empty, failure, no replay and actual cancellation retirement.
