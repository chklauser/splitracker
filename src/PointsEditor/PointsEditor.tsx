import {Fragment, FunctionComponent, ReactElement, useState} from "react";
import {Point} from "../PointDisplay";
import {PointsVec} from "../PointsBlock";
import {PointsPreview} from "../PointsPreview";
import "./PointsEditor.css";

interface IPointsEditorProps {
  baseCapacity: number;
  currentPoints: PointsVec;
  showPenalties: boolean;
}

interface PreviewData {
  currentTypeDisplay: ReactElement<any, any>;
  points: PointsVec;
  typeValue: number;
  minAmount: number;
  maxAmount: number;
  totalCurrentPoints: number;
}

function calculatePreview(type: Point, value: number, currentPoints: PointsVec, baseCapacity: number): PreviewData {
  let preview: Omit<PreviewData, "minAmount" | "maxAmount" | "totalCurrentPoints">;
  switch (type) {
    case Point.Consumed:
      preview = {
        currentTypeDisplay: <Fragment>Verzehrt 🕳️</Fragment>,
        points: {consumed: value, exhausted: 0, channeled: 0},
        typeValue: currentPoints.consumed
      };
      break;
    case Point.Channeled:
      preview = {
        currentTypeDisplay: <Fragment>Kanalisiert ⚡</Fragment>,
        points: {consumed: 0, exhausted: 0, channeled: value},
        typeValue: currentPoints.channeled
      };
      break;
    case Point.Exhausted:
      preview = {
        currentTypeDisplay: <Fragment>Erschöpft 💤</Fragment>,
        points: {consumed: 0, exhausted: value, channeled: 0},
        typeValue: currentPoints.exhausted
      };
      break;
    default:
      console.error("Invalid type state: ", type);
      preview = {
        currentTypeDisplay: <Fragment>???</Fragment>,
        points: {consumed: 0, exhausted: 0, channeled: 0},
        typeValue: 0
      };
      break;
  }
  const totalCurrentPoints = currentPoints.channeled + currentPoints.exhausted + currentPoints.consumed;
  const maxAmount = Math.max(0, baseCapacity * 5 - totalCurrentPoints);
  const minAmount = Math.min(0, -preview.typeValue);
  return {
    ...preview,
    maxAmount,
    minAmount,
    totalCurrentPoints
  }
}

export const PointsEditor: FunctionComponent<IPointsEditorProps> = ({
  baseCapacity,
  currentPoints,
  showPenalties
}) => {
  const [type, setType] = useState(Point.Consumed);
  const [value, setValue] = useState(0);
  const toggleType = () => {
    setType(ty => {
      let nextType: Point;
      switch (ty) {
        case Point.Consumed:
          nextType = Point.Channeled;
          break;
        case Point.Channeled:
          nextType = Point.Exhausted;
          break;
        case Point.Exhausted:
          nextType = Point.Consumed;
          break;
        default:
          console.error("Invalid type state: ", ty);
          nextType = ty;
          break;
      }
      const preview = calculatePreview(nextType, value, currentPoints, baseCapacity);
      setValue(v => Math.min(preview.maxAmount, Math.max(preview.minAmount, v)));
      return nextType;
    });
  };
  const totalCurrentPoints = currentPoints.channeled + currentPoints.exhausted + currentPoints.consumed;
  const preview = calculatePreview(type, value, currentPoints, baseCapacity);

  return <div className="PointsEditor">
    <div className="PointsEditor-controls">
      <button type="button" className="PointsEditor-switch" onClick={toggleType}><p>{preview.currentTypeDisplay}</p><p>(wechseln)</p></button>
      <input type="range" className="PointsEditor-range" min={preview.minAmount} max={preview.maxAmount} value={value}
             onChange={e => setValue(e.target.valueAsNumber)}
             step={1}/>
      <input type="number" className="PointsEditor-edit" min={preview.minAmount} max={preview.maxAmount} value={value}
           onChange={e => setValue(e.target.valueAsNumber)}/>
    </div>
    <PointsPreview {...{baseCapacity, totalCurrentPoints, showPenalties}}
                   points={preview.points}
                   onAppliedPoints={_ => setValue(_ => 0)}/>
  </div>;
};