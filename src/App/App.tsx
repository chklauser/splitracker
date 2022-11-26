import React, {FunctionComponent, useState} from 'react';
import {DndProvider} from "react-dnd";
import {HTML5Backend} from "react-dnd-html5-backend";
import {TouchBackend} from "react-dnd-touch-backend";
import {loadMouseEnabled, updateMouseEnabled} from "../preferences";
import {createDefaultCharacterPersistence} from "../char";
import {Card, Col, Container, FormCheck, Row, Spinner} from "react-bootstrap";
import "./App.scss";
import Gh from "../iconmonstr-github-1.svg";
import {createDefaultUndoPersistence, UndoManager} from "../undo";
import {UndoRedoControls} from "../UndoRedoControls";
import {MdCreate} from "react-icons/md";
import {firebaseApp, SignInState, StyledFirebaseAuth, uiConfig, useAuth} from "../firebaseSetup";
import {AppStage} from "../AppStage";
import {usePromiseFn} from "../asyncData";
import {cardSizing} from "../AppStage/AppStage";

const App: FunctionComponent = () => {
  const [mouseEnabled, setMouseEnabled] = useState(loadMouseEnabled());
  const [editMode, setEditMode] = useState(false);
  const [authVisible, setAuthVisible] = useState(false);
  const {signInState, uid} = useAuth();
  const {data: undoManager} = usePromiseFn(async () => {
    if (!uid) {
      console.log("Cannot create undo manager without user session");
      return null;
    }
    console.log("initializing undo manager");
    const mgr = new UndoManager(createDefaultUndoPersistence(), await createDefaultCharacterPersistence(uid));
    console.log("UndoManager created for uid", uid);
    return mgr;
  }, [uid]);

  function toggleMouseEnabled() {
    setMouseEnabled(prevState => updateMouseEnabled(_ => !prevState));
  }

  console.log("app: ", signInState, authVisible, uid);

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
            {undoManager && <UndoRedoControls {...{undoManager, uid}}/>}
          </Col>
          <Col xs="1">
            <span className="App-modeToggle" role="button"
                  onClick={toggleMouseEnabled}>{mouseEnabled ? '🖱️' : '👆'}</span>
          </Col>
          <Col xs="1">
            <FormCheck type="switch" label={<MdCreate/>} checked={editMode}
                       onChange={e => setEditMode(e.target.checked)}/>
          </Col>
          <Col xs="1">
            <span className="App-authToggle" role="button" onClick={() => setAuthVisible(x => !x)}>🔐</span>
          </Col>
        </Row>
        {(signInState != SignInState.SignedIn && authVisible) &&
          <Row>
            <StyledFirebaseAuth uiConfig={uiConfig} firebaseAuth={firebaseApp.auth()}/>
          </Row>}
        {undoManager && <AppStage {...{undoManager, editMode, uid}} /> || <Row className="gx-1 px-2">
          <Col as={Card} body {...cardSizing}>
            <Spinner animation="border"/>
            <p>Verbinde mit&hellip;☁️</p>
          </Col>
        </Row>}
        <Row className="gx-1 px-2">
          <a href="https://github.com/chklauser/splitracker" id="ghlink"><img src={Gh} alt="Splitracker on GitHub"/></a>
        </Row>
      </Container>
    </DndProvider>
  );
}


export default App;
