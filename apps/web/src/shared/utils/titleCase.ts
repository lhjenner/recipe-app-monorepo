const minorWords = new Set([
  'a', 'an', 'and', 'as', 'at', 'but', 'by', 'for', 'in', 'nor', 'of', 'on', 'or', 'the', 'to', 'with',
]);

// Letters/digits, allowing inner apostrophes so "pak'n" and "baker's" stay one word.
const wordPattern = /[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*/gu;

/**
 * Formats a name for display. Only first letters are raised; existing capitals
 * (e.g. "BBQ") are kept, and minor joining words stay as written mid-name.
 */
export function toTitleCase(value: string): string {
  const words = [...value.matchAll(wordPattern)];
  let result = '';
  let cursor = 0;

  words.forEach((match, index) => {
    const word = match[0];
    const start = match.index;
    const isEdgeWord = index === 0 || index === words.length - 1;
    result += value.slice(cursor, start);
    result += !isEdgeWord && minorWords.has(word.toLocaleLowerCase())
      ? word
      : word.charAt(0).toLocaleUpperCase() + word.slice(1);
    cursor = start + word.length;
  });

  return result + value.slice(cursor);
}
