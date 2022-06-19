type Versioned<T> = { v: number, p: T };

export interface Persistence<T> {
  load(): T;
  store(item: T): void;
}

export type Migration<T> = { [version: number]: ((persisted: T) => T) };

export interface PersistenceLogic<T> {
  get version(): number;
  get key(): string;
  factory: () => T;
  migrate: Migration<T>;
}

export class LocalStoragePersistence<T> implements Persistence<T> {
  warnedAboutOlderVersion = false;
  constructor(private readonly logic: PersistenceLogic<T>) {
  }

  private loadInternal(): Versioned<T> {
    const itemKey = this.logic.key;
    const rawItem = localStorage.getItem(itemKey);
    if(rawItem) {
      const item: Versioned<T> = JSON.parse(rawItem);
      const migration = this.logic.migrate[item.v];
      if(migration) {
        return {v: item.v, p: migration(item.p)};
      }
      else {
        throw new Error(`Unexpected version number in ${itemKey}: ${item['v']}`);
      }
    } else {
      console.log(`${itemKey} not found in local storage.`);
      const newItem = { v: this.logic.version, p: this.logic.factory()};
      localStorage.setItem(itemKey, JSON.stringify(newItem));
      return newItem;
    }
  }

  load(): T {
    return this.loadInternal().p;
  }

  store(item: T): void {
    const current = this.loadInternal();
    if(current.v > this.logic.version) {
      if(!this.warnedAboutOlderVersion) {
        alert("Du verwendest eine ältere Version von Splitracker. Bitte lade die Seite neu.");
        this.warnedAboutOlderVersion = true;
      }
      throw new Error("Version number of persisted data is newer than what this version of the app supports. " +
        `Key: ${this.logic.key}, Supported version: ${this.logic.version}, Actual version: ${current.v}`);
    } else {
      localStorage.setItem(this.logic.key, JSON.stringify({v: this.logic.version, p: item}));
    }
  }
}