import {v4} from 'uuid';
import {LocalStoragePersistence, Migration, Persistence} from "./persistence";

export interface PointsVec {
  channeled: number;
  exhausted: number;
  consumed: number;
}

export class Pool {
  public baseCapacity: number;
  public points: PointsVec;
  public channellings: number[];

  constructor(baseCapacity: number, points: PointsVec = {channeled: 0, exhausted: 0, consumed: 0}, channellings: number[] = []) {
    this.baseCapacity = baseCapacity;
    this.points = points;
    this.channellings = channellings;
  }
}

export class Character {
  public readonly id: string;
  public name: string;
  public lp: Pool;
  public fo: Pool;

  constructor(id: string, name: string, lpBaseCapacity: number, foBaseCapacity: number,
    lpPoints: PointsVec = {
      channeled: 0, exhausted: 0, consumed: 0
    }, foPoints: PointsVec = {
      channeled: 0, exhausted: 0, consumed: 0
    }, lpChannellings: number[] = [], foChannellings: number[] = []) {
    this.id = id;
    this.name = name;
    this.lp = new Pool(lpBaseCapacity, lpPoints, lpChannellings);
    this.fo = new Pool(foBaseCapacity, foPoints, foChannellings);
  }
}
export type Characters = Record<string, Character>


export const characterMigration: Migration<Character> = {
  1: (c: Character) => {
    c.lp.baseCapacity = Math.min(Math.max(1,c.lp.baseCapacity), 20);
    c.fo.baseCapacity = Math.min(Math.max(1,c.fo.baseCapacity), 5*12);
    return c;
  }
}

function mapObject<TKey extends string | number | symbol, TInput, TOutput>(object: Record<TKey, TInput>, mapFn: (input: TInput, key: TKey) => TOutput): Record<TKey, TOutput> {
  return Object.keys(object).reduce(function(result, key0) {
    const key = key0 as TKey;
    result[key] = mapFn(object[key], key);
    return result
  }, {} as Record<TKey, TOutput>);
}

export const createDefaultCharacterPersistence = (): Persistence<Characters> => new LocalStoragePersistence<Characters>({
  get version(): number {
    return 1;
  },
  get key(): string {
    return 'characters';
  },
  factory: () => {
    const id = v4();
    return  {[id]: new Character(id, 'Dein Charakter', 8,8) };
  },
  // lift the per-character migration to apply to a map of characters
  migrate: mapObject(characterMigration, (migration) => (characters) => {
    for (let charKey in characters) {
      const c = characters[charKey];
      characters[charKey] = migration(c);
    }
    return characters;
  }),
});



