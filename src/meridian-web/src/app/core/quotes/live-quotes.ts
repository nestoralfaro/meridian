import { Injectable, InjectionToken, inject, signal } from '@angular/core';
import type { MoneyResponse } from '../api';

/** One holding re-valued at the live price. Every figure is computed on the server. */
export interface LivePosition {
  readonly symbol: string;
  readonly price: MoneyResponse;
  readonly marketValue: MoneyResponse;
  readonly dayChange: MoneyResponse;
  readonly unrealizedGain: MoneyResponse;
  readonly unrealizedGainPercent: number | null;
}

export interface LiveUpdate {
  readonly asOf: string;
  readonly totalValue: MoneyResponse;
  readonly dayChange: MoneyResponse;
  readonly positions: readonly LivePosition[];
}

export type LiveStatus = 'connecting' | 'live' | 'paused' | 'off';

/** The slice of a SignalR connection the app uses; lets tests substitute a fake. */
export interface QuotesConnection {
  on(method: 'positions', handler: (update: LiveUpdate) => void): void;
  onreconnecting(handler: () => void): void;
  onreconnected(handler: () => void): void;
  onclose(handler: () => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(method: 'WatchAccount', accountId: string): Promise<boolean>;
}

export const QUOTES_CONNECTION_FACTORY = new InjectionToken<() => Promise<QuotesConnection>>(
  'QUOTES_CONNECTION_FACTORY',
  {
    providedIn: 'root',
    factory: () => async () => {
      // Loaded on demand: only account pages pay for the SignalR client.
      const signalR = await import('@microsoft/signalr');
      return new signalR.HubConnectionBuilder()
        .withUrl('/hubs/quotes', {
          // WebSockets only: no negotiate round trip, and no sticky sessions needed behind the ingress.
          transport: signalR.HttpTransportType.WebSockets,
          skipNegotiation: true,
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.None)
        .build() as unknown as QuotesConnection;
    },
  },
);

/** Live (simulated) values for the account being viewed. Provide it per page so it stops when the page goes. */
@Injectable()
export class LiveQuotes {
  private readonly connect = inject(QUOTES_CONNECTION_FACTORY);
  private connection: QuotesConnection | null = null;

  readonly status = signal<LiveStatus>('connecting');
  readonly latest = signal<LiveUpdate | null>(null);

  async watch(accountId: string): Promise<void> {
    try {
      const connection = await this.connect();
      this.connection = connection;
      connection.on('positions', (update) => {
        this.latest.set(update);
        this.status.set('live');
      });
      connection.onreconnecting(() => this.status.set('paused'));
      connection.onreconnected(() => void this.subscribe(accountId));
      connection.onclose(() => this.status.set(this.status() === 'off' ? 'off' : 'paused'));
      await connection.start();
      await this.subscribe(accountId);
    } catch {
      this.status.set('paused');
    }
  }

  async stop(): Promise<void> {
    this.status.set('off');
    await this.connection?.stop().catch(() => undefined);
    this.connection = null;
  }

  private async subscribe(accountId: string): Promise<void> {
    const watching = await this.connection!.invoke('WatchAccount', accountId);
    if (!watching) {
      this.status.set('off'); // not this client's account: nothing to show live
      await this.stop();
    }
  }
}
