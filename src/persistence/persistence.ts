import {PersistenceDriver} from "./driver";
import {firebaseApp} from "../firebaseSetup";

type Versioned<T> = { v: number, p: T };

export interface Persistence<T> {
  load(): Promise<T>;
  store(item: T): Promise<void>;
  onChange(callback: (item: T) => Promise<void>): () => void;
}

export type Migration<T> = { [version: number]: ((persisted: T) => T) };

export interface PersistenceLogic<T> {
  get version(): number;
  get key(): string;
  factory: () => T;
  migrate: Migration<T>;
}

class PersistenceImpl<T> implements Persistence<T> {
  private warnedAboutOlderVersion = false;
  readonly disconnect: () => void;
  constructor(private readonly logic: PersistenceLogic<T>, private readonly driver: PersistenceDriver) {
    this.disconnect = driver.onChange(logic.key, async (item: unknown) => {
      if(item == null) {
        console.info("Persistence not initialized.");
        return;
      }
      const versioned = item as Versioned<T>;
      await this.notifyChange(versioned.p);
    });
  }

  private async loadInternal(): Promise<Versioned<T>> {
    const itemKey = this.logic.key;
    const item = await this.driver.getItem(itemKey) as Versioned<T> | null;
    if (item) {
      const migration = this.logic.migrate[item.v];
      if (migration) {
        return {v: item.v, p: migration(item.p)};
      } else {
        throw new Error(`Unexpected version number in ${itemKey}: ${item['v']}`);
      }
    } else {
      console.log(`${itemKey} not found in persistence.`);
      const newItem = {v: this.logic.version, p: this.logic.factory()};
      await this.driver.setItem(itemKey, newItem);
      return newItem;
    }
  }

  async load(): Promise<T> {
    const user = firebaseApp.auth().currentUser;
    if(user) {
      console.log("load with id: ", user.uid, "anon", user.isAnonymous);
    } else {
      console.error("no user, not even an anonymous user")
    }
    return (await this.loadInternal()).p;
  }

  async store(item: T): Promise<void> {
    const current = await this.loadInternal();
    if(current.v > this.logic.version) {
      if(!this.warnedAboutOlderVersion) {
        alert("Du verwendest eine ältere Version von Splitracker. Bitte lade die Seite neu.");
        this.warnedAboutOlderVersion = true;
      }
      throw new Error("Version number of persisted data is newer than what this version of the app supports. " +
        `Key: ${this.logic.key}, Supported version: ${this.logic.version}, Actual version: ${current.v}`);
    } else {
      await this.driver.setItem(this.logic.key, {v: this.logic.version, p: item});
    }
  }

  private onChangeCallbacks: ((item: T) => Promise<void>)[] = []
  onChange(callback: (item: T) => Promise<void>): () => void {
    this.onChangeCallbacks.push(callback);
    return () => {
      this.onChangeCallbacks = this.onChangeCallbacks.filter(c => c !== callback);
    };
  }
  private async notifyChange(item: T): Promise<void> {
    console.log("Persistence.notifyChange(", item, ") ", this.onChangeCallbacks.length, " handlers");
    for(const callback of this.onChangeCallbacks) {
      await callback(item);
    }
  }
}

export function mkPersistence<T>(logic: PersistenceLogic<T>, driver: PersistenceDriver): Persistence<T> {
  return new PersistenceImpl(logic, driver);
}