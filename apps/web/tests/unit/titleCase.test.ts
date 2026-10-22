import { describe, expect, it } from 'vitest';
import { toTitleCase } from '../../src/titleCase';

describe('toTitleCase', () => {
  it.each([
    ['agar agar', 'Agar Agar'],
    ['baking aisle', 'Baking Aisle'],
    ['salt and pepper', 'Salt and Pepper'],
    ['fruit & veg', 'Fruit & Veg'],
    ['the end of', 'The End Of'],
    ['BBQ sauce', 'BBQ Sauce'],
    ["baker's flour", "Baker's Flour"],
    ["pak'n save", "Pak'n Save"],
    ['stir-fry sauce', 'Stir-Fry Sauce'],
    ['2 minute noodles (chicken)', '2 Minute Noodles (Chicken)'],
    ['crème fraîche', 'Crème Fraîche'],
    ['', ''],
  ])('formats %j as %j', (input, expected) => {
    expect(toTitleCase(input)).toBe(expected);
  });

  it('keeps spacing exactly as given', () => {
    expect(toTitleCase('  rice  noodles ')).toBe('  Rice  Noodles ');
  });
});
