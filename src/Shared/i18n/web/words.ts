// Library copy of shared/i18n 2.0.0. Do not edit: see src/Shared/i18n/VERSION.
// The page part of the i18n library. The C# code of a mod sends its mod texts in the display language; a page script
// reads each one through word, and fills its placeholders with fill. A page script holds no text of a mod text of its
// own, so a page part that shows mod texts draws only after the first data from C#.

// The text of a mod text that C# sent, or its name when the data has none (as I18nTextSet does in C#).
export function word(words: Record<string, string> | undefined, name: string): string {
  const text = words && Object.hasOwn(words, name) ? words[name] : '';
  return text || name;
}

// The text with each placeholder {name} replaced by its value, as I18nTexts.Fill does in C#. A placeholder with no
// value stays as it is.
export function fill(text: string, values: Record<string, string | number>): string {
  return String(text ?? '').replace(/\{([^}]*)\}/g, (all, name: string) =>
    name !== '' && Object.hasOwn(values, name) ? String(values[name]) : all,
  );
}
