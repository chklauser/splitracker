import React, {ChangeEventHandler, FunctionComponent, useContext} from 'react';
import {PointsVec} from "../char";
import {PointsEditor} from "../PointsEditor";
import {CurrentPoints} from "../CurrentPoints";
import {ShortRest} from "../ShortRest";
import {StopChanneling} from "../StopChanneling";
import {PointsPreviewData} from "../drag";
import "./PointsControl.scss";
import {Accordion, AccordionContext} from "react-bootstrap";

export interface IPointsControlProps {
  title: string;
  baseCapacityLabel: string;
  baseCapacity: number;
  lineCapacity: (baseCapcity: number) => number;
  totalCapacity: (baseCapcity: number) => number;
  maxBaseCapacity: number;
  points: PointsVec;
  onBaseCapacityChanged?: (newBaseCapacity: number) => void;
  showPenalties: boolean;
  onReceivePoints: (points: PointsPreviewData) => void;
  channellings: number[];
  eventKey: string;
}

export const PointsControl: FunctionComponent<IPointsControlProps> = ({
  title,
  baseCapacityLabel,
  onBaseCapacityChanged,
  baseCapacity,
  lineCapacity,
  totalCapacity,
  maxBaseCapacity,
  points,
  showPenalties,
  onReceivePoints,
  channellings,
  eventKey
}) => {
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

  const { activeEventKey } = useContext(AccordionContext);
  const penalty = Math.min(Math.floor(Math.pow(2, Math.ceil((points.exhausted + points.consumed + points.channeled) / baseCapacity) - 2)), 8);

  return (
    <Accordion.Item eventKey={eventKey}>
      <Accordion.Header>
        <span className="PointsControl-title col">{title}</span>
        <span
          className="PointsControl-value col-1">{totalCapacity(baseCapacity) - (points.exhausted + points.consumed + points.channeled)}</span>
        {showPenalties ?
          <abbr title="Wundabzüge" className="PointsControl-penalty col-1">({penalty > 0 ? '-' : '±'}{penalty})</abbr> : <span className="PointsControl-penalty"/> }
        <label className="PointsControl-baseCapacity col-3" style={{visibility: activeEventKey === eventKey ? 'visible' : 'hidden'}}>
          <span>{baseCapacityLabel}</span>
          <input type="number" value={baseCapacity}
                 min={1} max={maxBaseCapacity}
                 onChange={onBaseCapacityChangedInternal}
                 onClick={(e) => e.stopPropagation()}
          />
        </label>
      </Accordion.Header>
      <Accordion.Body>
        <CurrentPoints {...{points, showPenalties, onReceivePoints}}
                       lineCapacity={lineCapacity(baseCapacity)}
                       totalCapcity={totalCapacity(baseCapacity)}
        />
        <PointsEditor {...{baseCapacity, showPenalties, onReceivePoints}}
                      lineCapacity={lineCapacity(baseCapacity)}
                      totalCapcity={totalCapacity(baseCapacity)}
                      currentPoints={points}/>
        {channellings.map((channeled, index) =>
          <StopChanneling channeled={channeled} index={index} key={`${channeled}-${index}`}/>
        )}
        <ShortRest currentPoints={points}/>
      </Accordion.Body>
    </Accordion.Item>
  );
}
