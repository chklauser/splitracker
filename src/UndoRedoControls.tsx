import type {UndoManager} from "./undo";
import {FunctionComponent, useEffect, useState} from "react";
import {Col, Row} from "react-bootstrap";
import {MdRedo, MdUndo} from "react-icons/md";
import {useDeferredFn} from "./asyncData/asyncData";

export type IUndoRedoControlsProps = {
  undoManager: UndoManager;
  uid: string | null;
}
type UndoRedoState = {
  canUndo: boolean;
  canRedo: boolean;
}

function toUndoRedoState(undoManager: UndoManager): UndoRedoState {
  return {
    canUndo: undoManager.canUndo,
    canRedo: undoManager.canRedo
  };
}

function useUndoRedo(undoManager: UndoManager): UndoRedoState {
  const [state, setState] = useState<UndoRedoState>(() => toUndoRedoState(undoManager));
  useEffect(() => {
    const handler = () => setState(toUndoRedoState(undoManager));
    undoManager.on("change", handler);
    return () => {
      undoManager.off("change", handler);
    }
  });
  return state;
}

export const UndoRedoControls: FunctionComponent<IUndoRedoControlsProps> = ({undoManager, uid}) => {
  const undoRedoState = useUndoRedo(undoManager);
  const undo = useDeferredFn(() => undoManager.undo(), [uid]);
  const redo = useDeferredFn(() => undoManager.redo(), [uid]);

  return <Row className="justify-content-center">
    <Col xs={3}>
      <MdUndo onClick={undo}
              style={{visibility: undoRedoState.canUndo ? undefined : 'hidden'}} role="button"
              aria-label="undo"/>
    </Col>
    <Col xs={3}>
      <MdRedo onClick={redo}
              style={{visibility: undoRedoState.canRedo ? undefined : 'hidden'}} role="button"
              aria-label="redo"/>
    </Col>
  </Row>
};