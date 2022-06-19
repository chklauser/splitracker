import {Character, characterMigration, Characters} from "./char";
import {LocalStoragePersistence, Persistence} from "./persistence";

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

export const createDefaultUndoPersistence = (): Persistence<UndoBuffer> =>  new LocalStoragePersistence<UndoBuffer>({
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
});

export class UndoManager {
  private onChange: (() => void)[] = [];

  constructor(private readonly persistence: Persistence<UndoBuffer>, private readonly characterPersistence: Persistence<Characters>) {
  }

  on(event: "change", handler: () => void) {
    this.onChange.push(handler);
  }

  off(event: "change", handler: () => void) {
    this.onChange = this.onChange.filter(h => h !== handler);
  }

  addCharacter(newCharacter: Character) {
    this.performAction({action: "addCharacter", character: newCharacter});
  }

  removeCharacter(characterId: string) {
    this.performAction({action: "removeCharacter", characterId});
  }

  updateCharacter(updatedCharacter: Character) {
    this.performAction({action: "updateCharacter", character: updatedCharacter});
  }

  private addUndoAction(action: Action) {
    const buffer = this.persistence.load();
    buffer.undoActions.push(action);
    buffer.undoActions = buffer.undoActions.slice(-10);
    buffer.redoActions = [];
    console.log("add undo", buffer);
    this.persistence.store(buffer);
  }

  private performAction(forwardAction: Action) {
    const undoAction = this.invertedAction(forwardAction);
    this.addUndoAction(undoAction);
    this.applyAction(forwardAction);
    this.notifyChange();
  }

  undo() {
    const buffer = this.persistence.load();
    const undoAction = buffer.undoActions.pop();
    if (!undoAction) {
      console.log("No undo action to perform.");
      return;
    }
    let redoAction = this.invertedAction(undoAction);
    buffer.redoActions.push(redoAction);
    this.applyAction(undoAction);
    console.log("undo state: ", buffer);
    this.persistence.store(buffer);
    this.notifyChange();
  }

  redo() {
    const buffer = this.persistence.load();
    const redoAction = buffer.redoActions.pop();
    if (!redoAction) {
      console.log("No redo action to perform.");
      return;
    }
    let undoAction = this.invertedAction(redoAction);
    buffer.undoActions.push(undoAction);
    this.applyAction(redoAction);
    console.log("redo state: ", buffer);
    this.persistence.store(buffer);
    this.notifyChange();
  }

  private notifyChange() {
    this.onChange.forEach(handler => handler());
  }

  get canUndo(): boolean {
    return this.persistence.load().undoActions.length > 0;
  }

  get canRedo(): boolean {
    return this.persistence.load().redoActions.length > 0;
  }

  private applyAction(action: Action) {
    const characters = this.characterPersistence.load();
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
    this.characterPersistence.store(characters);
  }

  private invertedAction(forwardAction: Action) {
    let redoAction: Action;
    switch (forwardAction.action) {
      case "addCharacter":
        redoAction = {action: "removeCharacter", characterId: forwardAction.character.id};
        break;
      case "removeCharacter":
        redoAction = {action: "addCharacter", character: this.characterPersistence.load()[forwardAction.characterId]};
        break;
      case "updateCharacter":
        redoAction = {
          action: "updateCharacter",
          character: this.characterPersistence.load()[forwardAction.character.id]
        };
        break;
      default:
        throw new Error(`Unknown undo action: ${forwardAction['action']}`);
    }
    return redoAction;
  }
}