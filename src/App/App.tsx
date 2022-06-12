import React, {FunctionComponent, useState} from 'react';
import './App.css';
import {PointsControl} from "../PointsControl";
import {DndProvider} from "react-dnd";
import {HTML5Backend} from "react-dnd-html5-backend";
import {PointsVec} from "../PointsBlock";
import {PointsPreviewData} from "../drag";

function applyPointsReceived(
  updatePoints: (transform: (points: PointsVec) => PointsVec) => void,
  updateChannelings: UpdateChannelings,
  data: PointsPreviewData): void {
  const receivedPoints = data.points;
  updatePoints((currentPoints: PointsVec): PointsVec => {
    console.log("updatePoints", currentPoints, receivedPoints);
    return ({
      exhausted: Math.max(0, currentPoints.exhausted + receivedPoints.exhausted),
      consumed: Math.max(0, currentPoints.consumed + receivedPoints.consumed),
      channeled: Math.max(0, currentPoints.channeled + receivedPoints.channeled)
    });
  });

  if (receivedPoints.channeled > 0) {
    console.log("remember channeling of ", receivedPoints.channeled);
    updateChannelings((channelings: number[]) => channelings.concat([receivedPoints.channeled]));
  }

  if(data.channelingIndex) {
    console.log("remove channeling index ", data.channelingIndex);
    updateChannelings((channelings: number[]) => channelings.filter(c => c != data.channelingIndex));
  }
}

type UpdateChannelings = (transform: (channelings: number[]) => number[]) => void;

const App: FunctionComponent = () => {
  const [lp, setLp] = useState(8);
  const [lPoints, setlPoints] = useState<PointsVec>({exhausted: 3, consumed: 7, channeled: 2});
  const [lChannelings, setlChannelings] = useState<number[]>([2]);

  const [fo, setFo] = useState(6);
  const [fPoints, setfPoints] = useState<PointsVec>({exhausted: 2, consumed: 5, channeled: 6});
  const [fChannelings, setfChannelings] = useState<number[]>([3, 1, 2]);

  const [focusOn, setFocusOn] = useState(null as 'fp' | 'lp' | null);

  return (
    <DndProvider backend={HTML5Backend} options={{
      enableTouchEvents: true,
      enableMouseEvents: true, // This is buggy due to the difference in touchstart/touchend event propagation compared to mousedown/mouseup/click.
      touchSlop: 25,
      ignoreContextMenu: true,
      scrollAngleRanges: [
        // dragging between these angles is ignored
        // degrees move clockwise, 0/360 pointing to the left.
        {start: 30, end: 150},
        {start: 210, end: 330}
      ],
      enableHoverOutsideTarget: true
    }}>
      <div className="App">
        <PointsControl points={lPoints} baseCapacity={lp}
                       onBaseCapacityChanged={setLp}
                       baseCapacityLabel="LP" title="Lebenspunkte 💖"
                       expanded={focusOn == 'lp'}
                       onToggleExapanded={(expanded) => setFocusOn(focusOn => expanded ? 'lp' : focusOn != 'fp' ? null : 'fp')}
                       showPenalties={true}
                       onReceivePoints={(points) => applyPointsReceived(setlPoints, setlChannelings, points)}
                       channellings={lChannelings}

        />
        <PointsControl title="Fokuspunkte ✨" baseCapacityLabel="FP" onBaseCapacityChanged={setFo}
                       baseCapacity={fo}
                       points={fPoints}
                       expanded={focusOn == 'fp'}
                       onToggleExapanded={(expanded) => setFocusOn(focusOn => expanded ? 'fp' : focusOn != 'lp' ? null : 'lp')}
                       showPenalties={false}
                       onReceivePoints={(points) => applyPointsReceived(setfPoints, setfChannelings, points)}
                       channellings={fChannelings}
        />
      </div>
    </DndProvider>
  );
}


export default App;
