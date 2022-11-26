import {Character, characterMigration, Characters} from "./char";
import {localStorageDriver, mkPersistence, Persistence} from "./persistence";

// the 'action' describes what happened in the forward direction
type Action = {
  action: "addCharacter"
  character: Character
} | {
  action: "removeCharacter"
  characterId: string
} | {
  action: "updateCharacter"
  character: Character
}

type UndoBuffer = {
  undoActions: Action[];
  redoActions: Action[];
}

export const createDefaultUndoPersistence = (): Persistence<UndoBuffer> =>  mkPersistence<UndoBuffer>({
  get version(): number {
    return 1
  },
  get key(): string {
    return "undo";
  },
  factory: () => {
    return {undoActions: [], redoActions: []};
  },
  migrate: {
    1: (buffer) => {
      const charMigration = characterMigration[1];
      if (!charMigration) {
        throw new Error("Undo buffer needs to have a migration for character version 1");
      }
      const migrateActions = (actions: Action[]): Action[] => actions.map(action => {
        switch (action.action) {
          case "addCharacter":
          case "updateCharacter":
            return {action: action.action, character: charMigration(action.character)};
          case "removeCharacter":
            return action;
          default:
            throw new Error(`Unknown undo action: ${action['action']} in v1`);
        }
      });
      return {
        undoActions: migrateActions(buffer.undoActions),
        redoActions: migrateActions(buffer.redoActions)
      };
    }
  }
}, localStorageDriver);

export class UndoManager {
  private onChange: (() => void)[] = [];
  canRedo: boolean = false;
  canUndo: boolean = false;

  constructor(private readonly persistence: Persistence<UndoBuffer>, public readonly characterPersistence: Persistence<Characters>) {
    this.disconnectActions.push(this.persistence.onChange(buf => {
      this.canUndo = buf.undoActions.length > 0;
      this.canRedo = buf.redoActions.length > 0;
      return Promise.resolve();
    }));
    this.disconnectActions.push(this.characterPersistence.onChange(async () => {
      console.log("forward character persistence change")
      await this.notifyChange();
    }));
  }

  private readonly disconnectActions: (() => void)[] = [];
  disconnect(): void {
    this.disconnectActions.forEach(action => action());
  }

  on(event: "change", handler: () => void|Promise<void>) {
    this.onChange.push(handler);
  }

  off(event: "change", handler: () => void|Promise<void>) {
    this.onChange = this.onChange.filter(h => h !== handler);
  }

  async addCharacter(newCharacter: Character): Promise<void> {
    await this.performAction({action: "addCharacter", character: newCharacter});
  }

  async removeCharacter(characterId: string): Promise<void> {
    await this.performAction({action: "removeCharacter", characterId});
  }

  async updateCharacter(updatedCharacter: Character): Promise<void> {
    await this.performAction({action: "updateCharacter", character: updatedCharacter});
  }

  private async addUndoAction(action: Action): Promise<void> {
    const buffer = await this.persistence.load();
    buffer.undoActions.push(action);
    buffer.undoActions = buffer.undoActions.slice(-10);
    buffer.redoActions = [];
    console.log("add undo", buffer);
    await this.persistence.store(buffer);
  }

  private async performAction(forwardAction: Action): Promise<void> {
    const undoAction = await this.invertedAction(forwardAction);
    await this.addUndoAction(undoAction);
    await this.applyAction(forwardAction);
    await this.notifyChange();
  }

  async undo(): Promise<void> {
    const buffer = await this.persistence.load();
    const undoAction = buffer.undoActions.pop();
    if (!undoAction) {
      console.log("No undo action to perform.");
      return;
    }
    let redoAction = await this.invertedAction(undoAction);
    buffer.redoActions.push(redoAction);
    await this.applyAction(undoAction);
    console.log("undo state: ", buffer);
    await this.persistence.store(buffer);
    await this.notifyChange();
  }

  async redo(): Promise<void> {
    const buffer = await this.persistence.load();
    const redoAction = buffer.redoActions.pop();
    if (!redoAction) {
      console.log("No redo action to perform.");
      return;
    }
    let undoAction = await this.invertedAction(redoAction);
    buffer.undoActions.push(undoAction);
    await this.applyAction(redoAction);
    console.log("redo state: ", buffer);
    await this.persistence.store(buffer);
    await this.notifyChange();
  }

  private async notifyChange() {
    console.log("UndoManager.notifyChange", this.onChange.length, "handlers")
    for (let handler of this.onChange) {
      await handler();
    }
  }

  private async applyAction(action: Action): Promise<void> {
    const characters = await this.characterPersistence.load();
    switch (action.action) {
      case "removeCharacter":
        delete characters[action.characterId];
        break;
      case "addCharacter":
      case "updateCharacter":
        characters[action.character.id] = action.character;
        break;
      default:
        throw new Error(`Unknown undo action: ${action['action']}`);
    }
    await this.characterPersistence.store(characters);
  }

  private async invertedAction(forwardAction: Action): Promise<Action> {
    let redoAction: Action;
    switch (forwardAction.action) {
      case "addCharacter":
        redoAction = {action: "removeCharacter", characterId: forwardAction.character.id};
        break;
      case "removeCharacter":
        redoAction = {action: "addCharacter", character: (await this.characterPersistence.load())[forwardAction.characterId]};
        break;
      case "updateCharacter":
        redoAction = {
          action: "updateCharacter",
          character: (await this.characterPersistence.load())[forwardAction.character.id]
        };
        break;
      default:
        throw new Error(`Unknown undo action: ${forwardAction['action']}`);
    }
    return redoAction;
  }
}