# Existing-host authentication

This sample depends only on `SereinFlow.AspNetCore`. It does not register or
load `SereinFlow.Security.ApiKeys`. The host supplies an authenticated identity
with a `sub` or `ClaimTypes.NameIdentifier` claim, resource policies, and project
visibility. Replace the demonstration cookie registration and claim mapping
with the existing application's identity and permission services. The sample
intentionally has no second login system or token-issuing endpoint.

Set `SereinFlow:DataRoot` and `SereinFlow:WorkerRunnerPath` for a source checkout.
Published NuGet consumers receive the bundled Worker Runner automatically.
The browser uses the existing host session; clients may use their own
`HttpClient` handler or the SDK's `BearerTokenProvider`.
