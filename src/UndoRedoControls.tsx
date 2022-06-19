import type {UndoManager} from "./undo";
import {FunctionComponent, useEffect, useState} from "react";
import {Col, Row} from "react-bootstrap";
import {MdRedo, MdUndo} from "react-icons/md";

export type IUndoRedoControlsProps = {
  undoManager: UndoManager;
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

export const UndoRedoControls: FunctionComponent<IUndoRedoControlsProps> = ({undoManager}) => {
  const undoRedoState = useUndoRedo(undoManager);

  return <Row className="justify-content-center">
    <Col xs={3}>
      <MdUndo onClick={() => undoManager.undo()}
              style={{visibility: undoRedoState.canUndo ? undefined : 'hidden'}} role="button"
              aria-label="undo"/>
    </Col>
    <Col xs={3}>
      <MdRedo onClick={() => undoManager.redo()}
              style={{visibility: undoRedoState.canRedo ? undefined : 'hidden'}} role="button"
              aria-label="redo"/>
    </Col>
  </Row>
};