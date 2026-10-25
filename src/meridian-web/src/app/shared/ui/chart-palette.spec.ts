import { ASSET_CLASS_COLORS, SURFACES, contrastRatio } from './chart-palette';

describe('chart palette', () => {
  for (const theme of ['light', 'dark'] as const) {
    it(`every asset-class color has at least 3:1 contrast against the ${theme} surface (WCAG 1.4.11)`, () => {
      for (const [assetClass, color] of Object.entries(ASSET_CLASS_COLORS[theme])) {
        expect(
          contrastRatio(color, SURFACES[theme]),
          `${assetClass} ${color}`,
        ).toBeGreaterThanOrEqual(3);
      }
    });

    it(`uses four distinct colors in the ${theme} theme`, () => {
      expect(new Set(Object.values(ASSET_CLASS_COLORS[theme])).size).toBe(4);
    });
  }

  it('computes WCAG contrast ratios', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 1);
    expect(contrastRatio('#ffffff', '#ffffff')).toBeCloseTo(1, 5);
  });
});
