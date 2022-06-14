import React, {FunctionComponent, ReactElement, useState} from 'react';
import './App.css';
import {PointsControl} from "../PointsControl";
import {DndProvider} from "react-dnd";
import {HTML5Backend} from "react-dnd-html5-backend";
import {PointsPreviewData} from "../drag";
import {TouchBackend} from "react-dnd-touch-backend";
import {loadMouseEnabled, updateMouseEnabled} from "../preferences";
import {Character, loadCharacters, persistCharacter, Pool} from "../char";
import cloneDeep from "lodash.clonedeep";


function copyWith<T>(update: (value: T) => void): (value: T) => T {
  return (value: T) => {
    const copy = cloneDeep(value);
    update(copy);
    return copy;
  }
}

function useCharacter(): [Character, React.Dispatch<React.SetStateAction<Character>>] {
  const [char, setChar] = useState(() => {
    const chars = loadCharacters();
    return chars[Object.keys(chars)[0]];
  });

  return [char, (newValue) => {
    const effectiveValue = newValue instanceof Function ? newValue(char) : newValue;
    persistCharacter(effectiveValue);
    setChar(effectiveValue);
  }];
}

const App: FunctionComponent = () => {
  const [mouseEnabled, setMouseEnabled] = useState(loadMouseEnabled());
  const [char, setChar] = useCharacter();
  const [focusOn, setFocusOn] = useState(null as 'fp' | 'lp' | null);

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

  function pointsControl(poolOf: (char: Character) => Pool, title: string, baseCapacityLabel: string, showPenalties: boolean, focus: "lp"|"fp", otherFocus: "lp"|"fp"): ReactElement {
    const pool = poolOf(char);
    return <PointsControl points={pool.points} baseCapacity={pool.baseCapacity} channellings={pool.channellings}
                   onBaseCapacityChanged={newCap => setChar(copyWith(char => { poolOf(char).baseCapacity = newCap;}))}
                   baseCapacityLabel={baseCapacityLabel} title={title}
                   expanded={focusOn == focus}
                   onToggleExapanded={(expanded) => setFocusOn(focusOn => expanded ? focus : focusOn != otherFocus ? null : otherFocus)}
                   showPenalties={showPenalties}
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
      <div className="App">
        <div>
          <p className="App-title">Splitracker <span className="App-modeToggle"
                                                     onClick={toggleMouseEnabled}>{mouseEnabled ? '🖱️' : '👆'}</span>
          </p>
        </div>

        {pointsControl(c => c.lp, "Lebenspunkte 💖", "LP", true, "lp", "fp")}
        {pointsControl(c => c.fo, "Fokuspunkte ✨", "FP", false, "fp", "lp")}
        <p>ℹ️ Tipp: Elemente unten packen und auf die Punkte im oberen Bereich ziehen! 🤚</p>
      </div>
    </DndProvider>
  );
}


export default App;
