import type { GroceryItem } from '../services/groceryListApi';

type Contribution = GroceryItem['contributions'][number];

export function groupRecipeSources(contributions: Contribution[]): Contribution[][] {
  const sources = new Map<string, Contribution[]>();
  for (const contribution of contributions) {
    const key = contribution.origin === 'recipe' && contribution.recipeId
      && !contribution.unresolvedReason && contribution.canonicalUnit
      && contribution.interpretedAmount !== null
      && parseOriginalAmount(contribution.originalAmountText) !== null
      ? JSON.stringify([contribution.recipeId, contribution.state, contribution.canonicalUnit])
      : contribution.id;
    const source = sources.get(key);
    if (source) {
      source.push(contribution);
    } else {
      sources.set(key, [contribution]);
    }
  }
  return [...sources.values()];
}

export function formatSourceAmount(entries: Contribution[]): string {
  const first = entries[0];
  if (entries.length === 1) {
    return [first.originalAmountText, first.originalUnitText].filter(Boolean).join(' ');
  }
  const originalUnit = first.originalUnitText?.trim();
  const sameUnit = Boolean(originalUnit) && entries.every((entry) =>
    entry.originalUnitText?.trim().toLowerCase() === originalUnit?.toLowerCase());
  const total = entries.reduce((sum, entry) =>
    sum + (sameUnit ? parseOriginalAmount(entry.originalAmountText)! : entry.interpretedAmount!), 0);
  return `${new Intl.NumberFormat('en-NZ', { maximumFractionDigits: 9 }).format(total)} ${
    sameUnit ? originalUnit : first.canonicalUnit}`;
}

function parseOriginalAmount(text: string | null): number | null {
  const value = text?.trim() ?? '';
  let amount: number;
  if (/^[0-9]+(?:\.[0-9]+)?$/.test(value)) {
    amount = Number(value);
  } else {
    const fraction = /^(?:([0-9]+)\s+)?([0-9]+)\s*\/\s*([0-9]+)$/.exec(value);
    if (!fraction || Number(fraction[3]) === 0) {
      return null;
    }
    amount = Number(fraction[1] ?? 0) + Number(fraction[2]) / Number(fraction[3]);
  }
  return Number.isFinite(amount) && amount > 0 ? amount : null;
}
