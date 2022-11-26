import type {PersistenceDriver} from "./driver";

class LocalStorageDriver implements PersistenceDriver {
  getItem(key: string): Promise<unknown|null> {
    const raw = localStorage.getItem(key);
    if(raw) {
      return Promise.resolve(JSON.parse(raw));
    } else {
      return Promise.resolve(null);
    }
  }

  async setItem(key: string, value: unknown): Promise<void> {
    localStorage.setItem(key, JSON.stringify(value));
    await this.notifyChange(key, value);
  }
  
  private onChangeCallbacks: Record<string, ((item: unknown) => Promise<void>)[]> = {};
  onChange(key: string, callback: (item: unknown) => Promise<void>): () => void {
    let chain = this.onChangeCallbacks[key];
    if(!chain) {
      chain = [];
      this.onChangeCallbacks[key] = chain;
    }
    chain.push(callback);

    return () => {
      const chain = this.onChangeCallbacks[key];
      if(chain) {
        this.onChangeCallbacks[key] = chain.filter(cb => cb !== callback);
      }
    };
  }
  async notifyChange(key: string, item: unknown): Promise<void> {
    const chain = this.onChangeCallbacks[key];
    if(chain) {
      for(const cb of chain) {
        await cb(item);
      }
    }
  }

  disconnect(): void {
  }
}

export const localStorageDriver = new LocalStorageDriver();
