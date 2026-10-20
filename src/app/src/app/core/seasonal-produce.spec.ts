import { describe, expect, it } from 'vitest';
import { isInSeason, seasonalMatches } from './seasonal-produce';

describe('seasonal matching', () => {
  it('matches an exact produce tag in the right month', () => {
    expect(seasonalMatches(['jordbær'], 6)).toEqual(['jordbær (tidlige)']);
    expect(seasonalMatches(['jordbær'], 7)).toEqual(['jordbær']);
  });

  it('matches Danish singular/plural forms', () => {
    expect(seasonalMatches(['kartoffel'], 1)).toEqual(['kartofler']);
    expect(seasonalMatches(['æble'], 10)).toEqual(['æbler']);
    expect(seasonalMatches(['gulerod'], 1)).toEqual(['gulerødder']);
  });

  it('strips qualifiers from the produce label', () => {
    expect(seasonalMatches(['rabarber'], 3)).toEqual(['rabarber (slut)']);
    expect(seasonalMatches(['kartofler'], 5)).toEqual(['nye kartofler (fra sidst)']);
  });

  it('matches compound tags by prefix', () => {
    expect(seasonalMatches(['tomatsauce'], 7)).toEqual(['tomater']);
    expect(seasonalMatches(['løgsuppe'], 1)).toEqual(['løg']);
  });

  it('ignores tags of other seasons', () => {
    expect(seasonalMatches(['jordbær'], 12)).toEqual([]);
  });

  it('does not fuzz short words into a match', () => {
    expect(seasonalMatches(['pore'], 2)).toEqual([]);
    expect(seasonalMatches(['suppe'], 2)).toEqual([]);
  });

  it('is in season for the current month by default', () => {
    expect(isInSeason(['jordbær'], 6)).toBe(true);
    expect(isInSeason([], 6)).toBe(false);
  });
});
