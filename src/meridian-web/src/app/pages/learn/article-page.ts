import { DatePipe } from '@angular/common';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import type { ArticleResponse } from '../../core/api';

/**
 * One article. The HTML is rendered from reviewed Markdown and sanitized on the server; Angular's [innerHTML]
 * sanitizes it again in the browser.
 */
@Component({
  selector: 'mer-article-page',
  imports: [RouterLink, DatePipe],
  template: `
    <p class="back"><a routerLink="/learn">← Learn</a></p>
    @if (article.hasValue()) {
      @let a = article.value();
      <article>
        <h1 tabindex="-1">{{ a.summary.title }}</h1>
        <p class="meta">
          {{ a.summary.readingMinutes }} min read ·
          <time [attr.datetime]="a.summary.published">{{
            a.summary.published | date: 'MMM d, y' : 'UTC'
          }}</time>
        </p>
        <div class="prose" [innerHTML]="a.html"></div>
      </article>
    } @else if (notFound()) {
      <h1>Article not found</h1>
      <p>It may have moved. <a routerLink="/learn">See all articles</a>.</p>
    } @else if (article.error()) {
      <h1>Learn</h1>
      <p role="alert">Sorry, we couldn't load this article. Please try again later.</p>
    } @else {
      <h1>Learn</h1>
      <p aria-busy="true">Loading…</p>
    }
  `,
  styles: `
    .meta {
      color: var(--color-text-muted);
      font-size: var(--font-size-sm);
    }
    .prose {
      max-inline-size: 42rem;
      line-height: 1.6;
    }
    .prose ::ng-deep blockquote {
      margin-inline: 0;
      padding-inline-start: var(--space-4);
      border-inline-start: 4px solid var(--color-border-strong);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ArticlePage {
  /** From the route (`/learn/:slug`). */
  readonly slug = input.required<string>();

  protected readonly article = httpResource<ArticleResponse>(
    () => `/api/v1/learn/articles/${encodeURIComponent(this.slug())}`,
  );

  protected readonly notFound = computed(() => {
    const error = this.article.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  constructor() {
    const title = inject(Title);
    const document = inject(DOCUMENT);
    const injector = inject(Injector);
    effect(() => {
      if (this.article.hasValue()) {
        title.setTitle(`${this.article.value().summary.title} | Meridian`);
        // The heading replaces the loading one, so move focus to it once it's rendered.
        afterNextRender(() => document.querySelector<HTMLElement>('#main-content h1')?.focus(), {
          injector,
        });
      }
    });
  }
}
