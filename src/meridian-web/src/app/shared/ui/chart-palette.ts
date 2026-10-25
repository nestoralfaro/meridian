/**
 * Chart colors per asset class and theme. Each meets at least 3:1 contrast against the card surface (WCAG 1.4.11
 * non-text contrast); segments are separated by a surface-colored gap, so every boundary is visible.
 * Verified by chart-palette.spec.ts.
 */
export type AssetClassName = 'Fund' | 'Bond' | 'Cash' | 'Stock';
export type Theme = 'light' | 'dark';

export const SURFACES: Record<Theme, string> = { light: '#ffffff', dark: '#161b22' };

export const ASSET_CLASS_COLORS: Record<Theme, Record<AssetClassName, string>> = {
  light: { Fund: '#0b5cad', Bond: '#8250df', Cash: '#1a7f37', Stock: '#bc4c00' },
  dark: { Fund: '#6cb6ff', Bond: '#d2a8ff', Cash: '#56d364', Stock: '#ffa657' },
};

export const ASSET_CLASS_LABELS: Record<AssetClassName, string> = {
  Fund: 'Funds',
  Bond: 'Bonds',
  Cash: 'Cash',
  Stock: 'Stocks',
};

/** WCAG 2.x contrast ratio between two #rrggbb colors. */
export function contrastRatio(foreground: string, background: string): number {
  const [lighter, darker] = [relativeLuminance(foreground), relativeLuminance(background)].sort(
    (a, b) => b - a,
  ) as [number, number];
  return (lighter + 0.05) / (darker + 0.05);
}

function relativeLuminance(hex: string): number {
  const channels = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
  const [r, g, b] = channels.map((c) =>
    c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4,
  ) as [number, number, number];
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}
