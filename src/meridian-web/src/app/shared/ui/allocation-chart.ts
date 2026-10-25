import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import type { AllocationSliceResponse } from '../../core/api';
import {
  ASSET_CLASS_COLORS,
  ASSET_CLASS_LABELS,
  type AssetClassName,
  type Theme,
} from './chart-palette';
import { formatMoney } from './money';

/** Gap between segments, in percent of the circumference (the circle's circumference is 100). */
const GAP = 0.6;

interface Segment {
  readonly assetClass: string;
  readonly color: string;
  readonly dasharray: string;
  readonly dashoffset: number;
}

/**
 * Asset allocation as a donut plus a data table. The table is the accessible representation (and useful to everyone);
 * the SVG is decorative and hidden from assistive technology.
 */
@Component({
  selector: 'mer-allocation-chart',
  templateUrl: './allocation-chart.html',
  styleUrl: './allocation-chart.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllocationChart {
  readonly slices = input.required<readonly AllocationSliceResponse[]>();

  private readonly theme = signal<Theme>('light');

  protected readonly rows = computed(() =>
    this.slices().map((slice) => ({
      label: ASSET_CLASS_LABELS[slice.assetClass as AssetClassName] ?? slice.assetClass,
      value: formatMoney(slice.value),
      percent: `${slice.percent.toFixed(2)}%`,
      color: this.colorOf(slice.assetClass),
    })),
  );

  protected readonly segments = computed<Segment[]>(() => {
    const slices = this.slices();
    const gap = slices.length > 1 ? GAP : 0;
    let start = 0;
    return slices.map((slice) => {
      const length = Math.max(slice.percent - gap, 0);
      // Start at 12 o'clock (offset 25) and go clockwise.
      const segment = {
        assetClass: slice.assetClass,
        color: this.colorOf(slice.assetClass),
        dasharray: `${length} ${100 - length}`,
        dashoffset: 25 - start,
      };
      start += slice.percent;
      return segment;
    });
  });

  constructor() {
    const media = inject(DOCUMENT).defaultView?.matchMedia?.('(prefers-color-scheme: dark)');
    if (media) {
      this.theme.set(media.matches ? 'dark' : 'light');
      media.addEventListener('change', (event) => this.theme.set(event.matches ? 'dark' : 'light'));
    }
  }

  private colorOf(assetClass: string): string {
    return ASSET_CLASS_COLORS[this.theme()][assetClass as AssetClassName] ?? 'currentColor';
  }
}
