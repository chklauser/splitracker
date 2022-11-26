import {PersistenceDriver} from "./driver";
import firebase from "firebase/app";

export class FirebaseDriver implements PersistenceDriver {
  constructor(private readonly uid: string, private readonly db: firebase.database.Database) {}

  private readonly snapshot: Record<string, { promise: Promise<unknown>, resolve: (value: unknown) => void, resolved: boolean} > = {};

  private physicalKey(key: string): string {
    return `${key}/${this.uid}`;
  }

  async setItem(key: string, value: unknown): Promise<void> {
    await this.db.ref(this.physicalKey(key)).set(value);
  }

  async getItem(key: string): Promise<unknown | null> {
    const snapshot = this.snapshot[key];
    if (snapshot) {
      return snapshot.promise;
    } else {
      let resolve: undefined|((value: unknown) => void) = undefined;
      const promise = new Promise(function(r) {
        resolve = r;
      });
      if(resolve === undefined) {
        throw "this is impossible";
      }
      this.snapshot[key] = {promise, resolve, resolved: false};
      this.subscribe(key);
    }
  }

  private readonly subscriptions: Record<string, (a: (firebase.database.DataSnapshot | null), b?: (string | null | undefined)) => any> = {};
  private subscribe(key: string) {
    if(this.subscriptions[key] != null) {
      return;
    }

    this.subscriptions[key] = this.db.ref(this.physicalKey(key)).on('value', snapshot => {
      const newValue = snapshot.val();
      const existingPromise = this.snapshot[key];
      if (existingPromise && !existingPromise.resolved) {
        existingPromise.resolve(newValue);
        existingPromise.resolved = true;
      } else {
        this.snapshot[key] = {
          promise: Promise.resolve(newValue), resolve: () => {
          }, resolved: true
        };
      }

      this.notifyChange(key, newValue).catch(e => {
        console.error("Error while handling onChange notifications triggered by a firebase realtime database change.", e);
      });
    });
  }

  private readonly changeHandlers: Record<string, ((value: (unknown | null)) => Promise<void>)[]> = {};
  onChange(key: string, callback: (value: (unknown | null)) => Promise<void>): () => void {
    this.subscribe(key);
    let chain = this.changeHandlers[key];
    if(!chain) {
      chain = [];
      this.changeHandlers[key] = chain;
    }
    chain.push(callback);
    return () => {
      this.changeHandlers[key] = chain.filter(c => c !== callback);
    }
  }

  private async notifyChange(key: string, value: unknown) {
    const chain = this.changeHandlers[key];
    if(chain) {
      for(const callback of chain) {
        await callback(value);
      }
    }
  }

  disconnect(): void {
    for (const key in this.subscriptions) {
      this.db.ref(this.physicalKey(key)).off('value', this.subscriptions[key]);
    }
  }
}