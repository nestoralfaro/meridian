# ADR-0007: Angular state with signals and resources; NgRx SignalStore deferred

- **Status:** Accepted
- **Date:** 2026-10-24
- **Spike:** #27 (time box: 4 hours)

## Question

The SPA will hold server data (accounts, positions, activity), streaming quotes over SignalR (Sprint 2), and form state. Are Angular's built-in signals and resources enough, or does **NgRx SignalStore** pay for itself?

## Findings

- In **Angular 22.2**, `signal`, `computed`, `linkedSignal`, `resource` and **`httpResource`** are all stable public API (`@publicApi`, no experimental or developer-preview tags), checked in the installed typings.
- `httpResource` covers the common server-state needs: loading, value, error and `hasValue()` as signals, reload, and request cancellation when inputs change, without hand-written RxJS subscriptions.
- `@ngrx/signals` 22.0.1 supports Angular 22 and adds structure (`withState`, `withComputed`, `withMethods`, entity helpers, devtools integration) at the cost of a dependency and a pattern the team must learn.
- Testing: `httpResource` works with `HttpTestingController`, plus `TestBed.tick()` to run the effect. An unanswered request keeps the app unstable (it's a pending task), which helps: tests must handle every request.

## Options

| Option | For | Against |
|---|---|---|
| **Signals + `httpResource` in feature services** | Zero dependencies; Angular-native; small surface; easy to test | Conventions must be agreed (below); no built-in devtools time travel |
| NgRx SignalStore | Consistent store shape across features; entity helpers; devtools | Extra dependency and abstraction for an app with a handful of features |
| Classic NgRx Store (actions/reducers/effects) | Mature, explicit event log | Heavy boilerplate; mostly superseded for new apps by signal-based state |

## Decision

**Signals and resources, in feature-scoped services.** NgRx SignalStore is deferred, not rejected.

Conventions:
1. **Server state** uses `httpResource` (or `resource` for non-HTTP sources) in an `@Injectable` service. Components read signals and never subscribe manually.
2. **Derived state** uses `computed`; **local, resettable state derived from inputs** uses `linkedSignal`.
3. Services expose **read-only signals** and mutate only through methods (`readonly accounts = this.resource.value;` plus `refresh()`).
4. **RxJS only where the data is a stream** (SignalR quotes, typeahead), bridged with `toSignal`.
5. Components are `OnPush` and zoneless; templates bind signals directly.

**Revisit when** three or more features share entity collections with the same CRUD and optimistic-update needs, or we need devtools time travel to debug state. Moving a feature service to a SignalStore is mechanical.

## Proof

`BuildInfoService` now uses `httpResource` and exposes `shortCommit` as a `computed` signal that yields `null` on failure instead of throwing. Its tests cover success and failure. The accounts dashboard (#20) applies the same pattern to real data.
