import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { ArticleListResponse } from '../../core/api';

/** The Learn center: published articles, newest first. Public: no sign-in needed. */
@Component({
  selector: 'mer-learn-page',
  imports: [RouterLink, DatePipe],
  template: `
    <h1>Learn</h1>
    <p class="intro">Short, plain-language guides to the ideas behind your accounts.</p>

    @if (articles.hasValue()) {
      <ul class="articles">
        @for (article of articles.value().items; track article.slug) {
          <li class="article">
            <h2>
              <a [routerLink]="['/learn', article.slug]">{{ article.title }}</a>
            </h2>
            <p>{{ article.summary }}</p>
            <p class="meta">
              {{ article.readingMinutes }} min read ·
              <time [attr.datetime]="article.published">{{
                article.published | date: 'MMM d, y' : 'UTC'
              }}</time>
            </p>
          </li>
        }
      </ul>
    } @else if (articles.error()) {
      <p role="alert">Sorry, we couldn't load the articles. Please try again later.</p>
    } @else {
      <p aria-busy="true">Loading articles…</p>
    }
  `,
  styles: `
    .intro,
    .meta {
      color: var(--color-text-muted);
    }
    .meta {
      font-size: var(--font-size-sm);
    }
    .articles {
      display: grid;
      gap: var(--space-4);
      margin: var(--space-4) 0 0;
      padding: 0;
      list-style: none;
    }
    .article {
      padding: var(--space-4);
      border: 1px solid var(--color-border);
      border-radius: var(--radius);
      background: var(--color-surface);

      h2 {
        margin: 0 0 var(--space-2);
        font-size: 1.25rem;
      }
      p {
        margin: var(--space-1) 0;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearnPage {
  protected readonly articles = httpResource<ArticleListResponse>(() => '/api/v1/learn/articles');
}
