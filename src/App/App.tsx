import React, {FunctionComponent, useEffect, useState} from 'react';
import {DndProvider} from "react-dnd";
import {HTML5Backend} from "react-dnd-html5-backend";
import {TouchBackend} from "react-dnd-touch-backend";
import {loadMouseEnabled, updateMouseEnabled} from "../preferences";
import {Character, createDefaultCharacterPersistence} from "../char";
import {Button, Card, Col, Container, FormCheck, Row} from "react-bootstrap";
import "./App.scss";
import Gh from "../iconmonstr-github-1.svg";
import {createDefaultUndoPersistence, UndoManager} from "../undo";
import {UndoRedoControls} from "../UndoRedoControls";
import {CharacterControl} from "../CharacterControl";
import {MdCreate, MdPersonAdd} from "react-icons/md";
import {v4} from "uuid";


const characterPersistence = createDefaultCharacterPersistence();
const undoManager = new UndoManager(createDefaultUndoPersistence(), characterPersistence);

const App: FunctionComponent = () => {
  function refresedhCharacterIds(): string[] {
    const characters = characterPersistence.load();
    return Object.keys(characters)
      .map(id => characters[id])
      .filter(c => !!c)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(c => c.id);
  }

  const [mouseEnabled, setMouseEnabled] = useState(loadMouseEnabled());
  const [editMode, setEditMode] = useState(false);
  const [characterIds, setCharacterIds] = useState<string[]>(refresedhCharacterIds);

  function toggleMouseEnabled() {
    setMouseEnabled(prevState => updateMouseEnabled(_ => !prevState));
  }

  useEffect(() => {
    const onChange = () => {
      const newIds = refresedhCharacterIds();
      setCharacterIds(newIds);
    };
    undoManager.on("change", onChange);
    return () => {
      undoManager.off("change", onChange);
    }
  });

  function addCharacter() {
    undoManager.addCharacter(new Character(v4(), `Charakter ${characterIds.length + 1}`, 8, 8));
  }

  const cardSizing = {
    xs: 12,
    sm: 10,
    md: 8,
    lg: 6,
    xl: 4,
    xxl: 3,
    className: "g-1"
  };

  return (
    <DndProvider backend={mouseEnabled ? HTML5Backend : TouchBackend} options={{
      enableTouchEvents: true,
      enableMouseEvents: true, // This is buggy due to the difference in touchstart/touchend event propagation compared to mousedown/mouseup/click.
      touchSlop: 25,
      ignoreContextMenu: true,
      scrollAngleRanges: [
        // dragging between these angles is ignored
        // degrees move clockwise, 0/360 pointing to the left.
        {start: 315, end: 225},
      ],
      enableHoverOutsideTarget: true
    }}>
      <Container className="App">
        <Row className="gx-1 px-2">
          <Col as="h1" className="App-title">Splitracker</Col>
          <Col xs="4" className="App-undo">
            <UndoRedoControls undoManager={undoManager}/>
          </Col>
          <Col xs="1">
            <span className="App-modeToggle" role="button"
                            onClick={toggleMouseEnabled}>{mouseEnabled ? '🖱️' : '👆'}</span>
          </Col>
          <Col xs="1">
            <FormCheck type="switch" label={<MdCreate />} checked={editMode} onChange={e => setEditMode(e.target.checked)} />
          </Col>
        </Row>
        <Row className="gx-1 px-2">
          {editMode && <Col as={Button} {...cardSizing} variant="success" onClick={addCharacter} >Hinzufügen <MdPersonAdd /></Col>}
          {characterIds.map(id =>
            <Col as={Card} body key={id} {...cardSizing}>
              <CharacterControl characterId={id} {...{characterPersistence, undoManager, editMode}} />
            </Col>)}
        </Row>
        {!editMode && <Row className="gx-1 px-2">
          <p>ℹ️ Tipp: Elemente unten packen und auf die Punkte im oberen Bereich ziehen! 🤚</p>
        </Row>}
        <Row className="gx-1 px-2">
          <a href="https://github.com/chklauser/splitracker" id="ghlink"><img src={Gh} alt="Splitracker on GitHub"/></a>
        </Row>
      </Container>
    </DndProvider>
  );
}


export default App;
