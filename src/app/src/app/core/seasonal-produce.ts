/**
 * Danish seasonal produce per month (0 = januar). Static by design —
 * AGENTS.md: seasonal DK produce is a TS constant, never DB rows.
 */
export const MONTH_NAMES_DA: string[] = [
  'januar',
  'februar',
  'marts',
  'april',
  'maj',
  'juni',
  'juli',
  'august',
  'september',
  'oktober',
  'november',
  'december',
];

export const MONTH_NAMES_EN: string[] = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

export const SEASONAL_PRODUCE: Record<number, string[]> = {
  0: ['gulerødder', 'porrer', 'hvidkål', 'rosenkål', 'rødbeder', 'kartofler', 'æbler', 'løg'],
  1: ['gulerødder', 'porrer', 'hvidkål', 'rosenkål', 'rødbeder', 'kartofler', 'æbler', 'løg'],
  2: ['hvidkål', 'grøn dk-kål', 'porrer', 'rødbeder', 'kartofler', 'løg', 'rabarber (slut)'],
  3: ['grøn dk-kål', 'rabarber', 'agurk (drivhus)', 'tomat (drivhus)', 'kartofler', 'løg'],
  4: ['rabarber', 'asparges (fra slut)', 'nye kartofler (fra sidst)', 'rapsolie', 'spinat', 'agurk (drivhus)', 'tomat (drivhus)'],
  5: ['nye kartofler', 'asparges', 'spinat', 'blomkål (drivhus)', 'rabarber', 'ærbær', 'jordbær (tidlige)'],
  6: ['jordbær', 'hindbær', 'rabarber', 'nye kartofler', 'rødgrød-bær', 'spinat', 'grønne bønner', 'agurker', 'tomater'],
  7: ['tomater', 'agurker', 'squash', 'blomkål', 'broccoli', 'hindbær', 'blåbær', 'plommer', 'gresskar blomst'],
  8: ['tomater', 'squash', 'porrer (tidlige)', 'blomkål', 'plommer', 'æbler (tidlige)', 'græskar (tidlige)', 'blåbær'],
  9: ['æbler', 'pærer', 'græskar', 'porrer', 'hvidkål', 'rødkål', 'rosenkål (tidlig)', 'svampe (skov)', 'blommer'],
  10: ['æbler', 'pærer', 'græskar', 'porrer', 'hvidkål', 'rødkål', 'rosenkål', 'rødbeder', 'svampe (skov)', 'gulerødder'],
  11: ['rosenkål', 'rødkål', 'hvidkål', 'gulerødder', 'porrer', 'rødbeder', 'æbler', 'svampe (skov)'],
};

export function seasonalForMonth(month1to12: number): string[] {
  return SEASONAL_PRODUCE[(month1to12 - 1) % 12] ?? [];
}

/**
 * Strips display qualifiers ("(drivhus)", "nye") and normalizes the couple of
 * spelling variants in the list, so matching only sees the produce word.
 */
function baseLabel(label: string): string {
  return label
    .toLowerCase()
    .replace(/\([^)]*\)/g, ' ')
    .replace(/\bnye\b|\bny\b/g, ' ')
    .replace(/gresskar/g, 'græskar')
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * Irregular Danish singular/plural forms. Explicit on purpose — stemming
 * "porrer" would also produce "pore" and match unrelated tags.
 */
const IRREGULAR: Record<string, string[]> = {
  gulerødder: ['gulerod'],
  porrer: ['porre'],
  rødbeder: ['rødbede'],
  kartofler: ['kartoffel'],
  æbler: ['æble'],
  agurker: ['agurk'],
  tomater: ['tomat'],
  pærer: ['pære'],
  plommer: ['plomme'],
  blommer: ['blomme'],
  svampe: ['svamp'],
  'grønne bønner': ['grøn bønne', 'bønne', 'bønner'],
  'grøn dk-kål': ['grønkål', 'grøn kål'],
};

/** Exact word match, with a guarded prefix so "tomatsauce" still counts. */
function tagMatches(tag: string, candidate: string): boolean {
  if (tag === candidate) {
    return true;
  }
  const words = tag.split(/[^a-zæøå0-9-]+/).filter(Boolean);
  return words.some((w) => w === candidate || (candidate.length >= 3 && w.startsWith(candidate)));
}

/**
 * Which of a month's produce items the recipe is tagged with. Returns the
 * display labels (e.g. "jordbær (tidlige)") so the page can explain the match.
 */
export function seasonalMatches(tags: string[], month1to12: number): string[] {
  const normalized = tags.map((t) => t.toLowerCase().trim()).filter(Boolean);
  if (normalized.length === 0) {
    return [];
  }

  const hits: string[] = [];
  for (const item of seasonalForMonth(month1to12)) {
    const label = baseLabel(item);
    const candidates = [label, ...(IRREGULAR[label] ?? [])];
    if (normalized.some((tag) => candidates.some((c) => tagMatches(tag, c)))) {
      hits.push(item);
    }
  }
  return hits;
}

/** True when any tag matches the produce list of the given (default: current) month. */
export function isInSeason(tags: string[], month1to12: number = new Date().getMonth() + 1): boolean {
  return seasonalMatches(tags, month1to12).length > 0;
}

