import React, {FunctionComponent, ReactElement, useEffect, useState} from 'react';
import {PointsControl} from "../PointsControl";
import {DndProvider} from "react-dnd";
import {HTML5Backend} from "react-dnd-html5-backend";
import {PointsPreviewData} from "../drag";
import {TouchBackend} from "react-dnd-touch-backend";
import {loadMouseEnabled, updateMouseEnabled} from "../preferences";
import {Character, createDefaultCharacterPersistence, Pool} from "../char";
import cloneDeep from "lodash.clonedeep";
import {Accordion, Col, Container, Row} from "react-bootstrap";
import "./App.scss";
import Gh from "../iconmonstr-github-1.svg";
import {createDefaultUndoPersistence, UndoManager} from "../undo";
import {UndoRedoControls} from "../UndoRedoControls";

function copyWith<T>(update: (value: T) => void): (value: T) => T {
  return (value: T) => {
    const copy = cloneDeep(value);
    update(copy);
    return copy;
  }
}

const characterPersistence = createDefaultCharacterPersistence();
const undoManager = new UndoManager(createDefaultUndoPersistence(), characterPersistence);

function useCharacter(): [Character, React.Dispatch<React.SetStateAction<Character>>] {
  const [char, setChar] = useState(() => {
    const chars = characterPersistence.load();
    return chars[Object.keys(chars)[0]];
  });

  useEffect(() => {
    const stateChanged = () => {
      setChar(characterPersistence.load()[char.id])
    };
    undoManager.on("change", stateChanged);
    return () => {
      undoManager.off("change", stateChanged);
    };
  }, [char]);

  return [char, (newValue) => {
    const effectiveValue = newValue instanceof Function ? newValue(char) : newValue;
    undoManager.updateCharacter(effectiveValue);
  }];
}

const capcity: Record<"lp" | "fp", {
  lineCapacity: (baseCapacity: number) => number,
  totalCapacity: (baseCapacity: number) => number
}> = {
  lp: {
    lineCapacity: (baseCapacity: number) => baseCapacity,
    totalCapacity: (baseCapacity: number) => baseCapacity * 5
  },
  fp: {
    lineCapacity: (_) => 12,
    totalCapacity: (baseCapacity: number) => baseCapacity
  }
};

const App: FunctionComponent = () => {
  const [mouseEnabled, setMouseEnabled] = useState(loadMouseEnabled());
  const [char, setChar] = useCharacter();

  function toggleMouseEnabled() {
    setMouseEnabled(prevState => updateMouseEnabled(_ => !prevState));
  }

  function applyPointsReceived(poolOf: (char: Character) => Pool, data: PointsPreviewData): void {
    const receivedPoints = data.points;
    setChar(copyWith(char => {
      const pool = poolOf(char);
      const currentPoints = pool.points;
      pool.points = {
        exhausted: Math.max(0, currentPoints.exhausted + receivedPoints.exhausted),
        consumed: Math.max(0, currentPoints.consumed + receivedPoints.consumed),
        channeled: Math.max(0, currentPoints.channeled + receivedPoints.channeled)
      };

      if (receivedPoints.channeled > 0) {
        console.log("remember channeling of ", receivedPoints.channeled);
        pool.channellings.push(receivedPoints.channeled);
      }

      if (data.channelingIndex) {
        console.log("remove channeling index ", data.channelingIndex);
        pool.channellings = pool.channellings.filter(c => c != data.channelingIndex);
      }
    }));
  }

  function pointsControl(poolOf: (char: Character) => Pool, title: string, baseCapacityLabel: string, showPenalties: boolean, focus: "lp" | "fp"): ReactElement {
    const pool = poolOf(char);
    return <PointsControl eventKey={focus} points={pool.points} baseCapacity={pool.baseCapacity}
                          channellings={pool.channellings}
                          onBaseCapacityChanged={newCap => setChar(copyWith(char => {
                            poolOf(char).baseCapacity = newCap;
                          }))}
                          maxBaseCapacity={focus === "lp" ? 20 : 12*5}
                          baseCapacityLabel={baseCapacityLabel} title={title}
                          showPenalties={showPenalties}
                          {...capcity[focus]}
                          onReceivePoints={(points) => applyPointsReceived(poolOf, points)}
    />;
  }


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
          <Col xs="1"><span className="App-modeToggle" role="button"
                            onClick={toggleMouseEnabled}>{mouseEnabled ? '🖱️' : '👆'}</span></Col>
        </Row>
        <Row>
          <Col xs={12} sm={10} md={8} lg={6} xl={4} xxl={3}>
            <Row className="gx-0">
              <Accordion flush>
                {pointsControl(c => c.lp, "Lebenspunkte 💖", "LP", true, "lp")}
                {pointsControl(c => c.fo, "Fokuspunkte ✨", "FP", false, "fp")}
              </Accordion>
            </Row>
          </Col>
        </Row>
        <Row className="gx-1 px-2">
          <p>ℹ️ Tipp: Elemente unten packen und auf die Punkte im oberen Bereich ziehen! 🤚</p>
        </Row>
        <Row className="gx-1 px-2">
          <a href="https://github.com/chklauser/splitracker" id="ghlink"><img src={Gh} alt="Splitracker on GitHub"/></a>
        </Row>
      </Container>
    </DndProvider>
  );
}


export default App;
