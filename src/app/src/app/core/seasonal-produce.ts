/**
 * Danish seasonal produce per month (0 = januar). Static by design —
 * AGENTS.md: seasonal DK produce is a TS constant, never DB rows.
 */
export const MONTH_NAMES: string[] = [
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
