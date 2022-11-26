export interface PersistenceDriver {
  onChange(key: string, callback: (value: unknown | null) => Promise<void>): () => void;
  setItem(key: string, value: unknown): Promise<void>;
  getItem(key: string): Promise<unknown | null>;
  disconnect(): void;
}