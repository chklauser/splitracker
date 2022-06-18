import {v4} from 'uuid';

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

const charactersKey = 'characters';

type Versioned<T> = { v: number, p: T };
export type Characters = Record<string, Character>;
function internalLoadCharacters(): Versioned<Characters> {
  const rawCharacters = localStorage.getItem(charactersKey);
  if(rawCharacters) {
    const characters: Versioned<Characters> = JSON.parse(rawCharacters);
    if(characters && characters['v'] !== 1) {
      console.log("Unexpected version number in characters: ", characters['v']);
    }
    for (let charKey in characters.p) {
      const c = characters.p[charKey];
      c.lp.baseCapacity = Math.min(Math.max(1,c.lp.baseCapacity), 20);
      c.fo.baseCapacity = Math.min(Math.max(1,c.fo.baseCapacity), 20);
    }
    return characters;
  } else {
    console.log('No characters found in local storage.');
    const id = v4();
    const newChars = { v: 1, p: {[id]: new Character(id, 'Dein Charakter', 8,8) }};
    localStorage.setItem(charactersKey, JSON.stringify(newChars));
    return newChars;
  }
}

let warnedAboutOlderVersion = false;

export function persistCharacter(char: Character) {
  const characters = internalLoadCharacters();
  if(characters.v !== 1) {
    if(!warnedAboutOlderVersion) {
      alert("Du verwendest eine ältere Version von Splitracker. Kann deinen Charakter nicht speichern.");
      warnedAboutOlderVersion = true;
    }
    return;
  }

  characters.p[char.id] = char;
  localStorage.setItem(charactersKey, JSON.stringify(characters));
}
export function loadCharacters(): Characters {
  return internalLoadCharacters().p;
}

