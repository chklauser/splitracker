export class PromiseSource<T, E = any> implements PromiseCell<T> {
  private _promise: Promise<T> = undefined!;
  private _reject: (reason: E) => void = undefined!;
  private _resolve: (value: T) => void = undefined!;
  private _resolved: boolean = false;
  constructor() {
    this.reset();
  }
  get promise(): Promise<T> {
    return this._promise;
  }
  get resolved(): boolean {
    return this._resolved;
  }
  resolve(value: T): void {
    this._resolve(value);
    if(this._resolved) {
      this._promise = Promise.resolve(value);
    }else {
      this._resolved = true;
    }
  }
  reject(reason: E): void {
    this._reject(reason);
    if(this._resolved) {
      this._promise = Promise.reject(reason);
    } else {
      this._resolved = true;
    }
  }
  reset(): void {
    this._promise = new Promise((resolve, reject) => {
      this._resolve = resolve;
      this._reject = reject;
    });
    this._resolved = false;
  }
}

export interface PromiseCell<T> {
  get promise(): Promise<T>;
  get resolved(): boolean;
}