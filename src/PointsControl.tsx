import React, {ChangeEventHandler, FunctionComponent, useRef} from 'react';
import {PointsVec} from "./PointsBlock";
import {PointsEditor} from "./PointsEditor";
import {CurrentPoints} from "./CurrentPoints";
import {ShortRest} from "./ShortRest";
import {StopChanneling} from "./StopChanneling";
import {PointsPreviewData} from "./drag";

export interface IPointsControlProps {
  title: string;
  baseCapacityLabel: string;
  baseCapacity: number;
  points: PointsVec;
  onToggleExapanded: (newState: boolean) => void;
  onBaseCapacityChanged?: (newBaseCapacity: number) => void;
  expanded: boolean;
  showPenalties: boolean;
  onReceivePoints: (points: PointsPreviewData) => void;
  channellings: number[];
}

export const PointsControl: FunctionComponent<IPointsControlProps> = ({
  onToggleExapanded,
  title,
  baseCapacityLabel,
  onBaseCapacityChanged,
  baseCapacity,
  points,
  expanded,
  showPenalties,
  onReceivePoints,
  channellings
}) => {
  const detailsRef = useRef<HTMLDetailsElement>(null);
  const onBaseCapacityChangedInternal: ChangeEventHandler<HTMLInputElement> = (e) => {
    const newValue = Number.parseInt(e.target.value);
    if (!Number.isNaN(newValue)) {
      if (onBaseCapacityChanged) {
        onBaseCapacityChanged(newValue);
      }
    } else {
      e.target.value = baseCapacity.toString();
    }
  }

  console.log()
  const penalty = Math.min(Math.floor(Math.pow(2, Math.ceil((points.exhausted + points.consumed + points.channeled) / baseCapacity) - 2)), 8);

  return (
    <details ref={detailsRef} className="PointsControl"
             onToggle={() => onToggleExapanded(detailsRef.current?.open ?? false)} open={expanded}>
      <summary>
        <span className="PointsControl-title">{title}</span>
        <span
          className="PointsControl-value">{baseCapacity * 5 - (points.exhausted + points.consumed + points.channeled)}</span>
        {showPenalties ?
          <span className="PointsControl-penalty">Wundabzüge: {penalty > 0 ? '-' : '±'}{penalty}</span> : null}
        <label style={{display: expanded ? undefined : 'none'}}>
          <span>{baseCapacityLabel}</span>
          <input type="number" className="PointsControl-baseCapacity" value={baseCapacity}
                 onChange={onBaseCapacityChangedInternal}/>
        </label>
      </summary>
      <CurrentPoints {...{baseCapacity, points, showPenalties, onReceivePoints}} />
      <PointsEditor {...{baseCapacity, showPenalties}}
                    currentPoints={points}/>
      {channellings.map((channeled, index) =>
        <StopChanneling channeled={channeled} key={`${channeled}-${index}`}/>
      )}
      <ShortRest currentPoints={points}/>
    </details>
  );
}
