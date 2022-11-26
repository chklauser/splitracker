import React, {Fragment, FunctionComponent, useEffect} from "react";
import {Button, Card, Col, Row, Spinner} from "react-bootstrap";
import {MdPersonAdd} from "react-icons/md";
import {CharacterControl} from "../CharacterControl";
import {UndoManager} from "../undo";
import {Character} from "../char";
import {v4} from "uuid";
import {useDeferredFn, usePromiseFn} from "../asyncData";
import {onRenderCallback} from "../profiler";


export interface AppStageProps {
  undoManager: UndoManager,
  editMode: boolean,
  uid: string | null
}

export const cardSizing = {
  xs: 12,
  sm: 10,
  md: 8,
  lg: 6,
  xl: 4,
  xxl: 3,
  className: "g-1"
};

export const AppStage: FunctionComponent<AppStageProps> = ({editMode, undoManager, uid}) => {
  const {data: characterIds, reload: reloadCharacterIds, isPending, error} = usePromiseFn(async () => {
      console.log("scheduling load of character IDs");
      const characters = await undoManager.characterPersistence.load();
      console.log("loaded characters (app stage)", characters);
      return Object.keys(characters)
        .map(id => characters[id])
        .filter(c => !!c)
        .sort((a, b) => a.name.localeCompare(b.name))
        .map(c => c.id);
    }, [uid]);
  const addCharacter = useDeferredFn(async () => {
      if(characterIds !== undefined) {
        await undoManager.addCharacter(new Character(v4(), `Charakter ${characterIds.length + 1}`, 8, 8));
      }
    }, [characterIds]);

  useEffect(() => {
    const onChange = () => {
      reloadCharacterIds();
    };
    undoManager.on("change", onChange);
    return () => {
      undoManager.off("change", onChange);
    }
  });

  return <Fragment>
    <Row className="gx-1 px-2">
      {editMode &&
        <Col as={Button} {...cardSizing} variant="success" onClick={addCharacter}>Hinzufügen <MdPersonAdd/></Col>}
      {characterIds && characterIds.map(id =>
        <Col as={Card} body key={id} {...cardSizing}>
          <React.Profiler id="char" onRender={onRenderCallback}>
            <CharacterControl characterId={id} {...{undoManager, editMode}} />
          </React.Profiler>
        </Col>)}
      {isPending && <Col as={Card} body {...cardSizing}>
        <p>Verbunden. 🔌</p>
        <Spinner animation="border" />
        <p>Lade Charaktere&hellip;🚂</p>
        </Col>}
      {error && <Col as={Card} body {...cardSizing}>
        <p>Error while loading characters 😔</p>
      </Col>}
    </Row>
    {!editMode && <Row className="gx-1 px-2">
      <p>ℹ️ Tipp: Elemente unten packen und auf die Punkte im oberen Bereich ziehen! 🤚</p>
    </Row>}
  </Fragment>;
}