export function classSet(classNames: unknown): string {
  let names = '';

  if (typeof classNames == 'object') {
    for (const name in classNames) {
      // @ts-ignore // presensce  of field is checked with .hadOwnProperty()
      if (!classNames.hasOwnProperty(name) || !classNames[name]) {
        continue;
      }
      names += name + ' ';
    }
  } else {
    for (let i = 0; i < arguments.length; i++) {
      // We should technically exclude 0 too, but for the sake of backward
      // compat we'll keep it (for now)
      if (arguments[i] == null) {
        continue;
      }
      names += arguments[i] + ' ';
    }
  }

  return names.trim();
}
