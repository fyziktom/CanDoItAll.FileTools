# SB05 Dependent-Flow Smoke
Command: `Playwright folder navigation, live filesystem refresh, file activation, and host action smoke`
ExitCode: 0

- Run label: `2026-07-11 SB03/SB04 -> SB05 downstream verification`.
- Result: **Pass**.

The real Sandbox exercised the upstream foundations rather than only rendering a static fixture:

1. the live filesystem source browsed root-confined files from the SB04 provider;
2. refreshed/current state rendered in both list and card projections;
3. folder activation navigated through the SB03 session;
4. file activation emitted `ItemInvoked` to the Sandbox host and did not navigate or execute an open effect;
5. descriptive item action emitted `ActionRequested` to the host with zero session action executions;
6. source/snapshot replacement guards prevented stale callbacks and action loads from crossing into the host.

This closes the dependent UI smoke left open by SB03 and SB04. It establishes the upstream browser-to-host seam required by SB07, but it does not claim FileInteraction UI implementation.
