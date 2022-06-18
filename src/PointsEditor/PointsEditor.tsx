import {Fragment, FunctionComponent, ReactElement, useState} from "react";
import {Point, PointDisplay} from "../PointDisplay";
import {PointsVec} from "../char";
import {PointsPreview} from "../PointsPreview";
import "./PointsEditor.scss";
import {Col, Container, Row} from "react-bootstrap";
import FormRange from "react-bootstrap/FormRange";

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
        currentTypeDisplay: <Fragment>V&nbsp;<PointDisplay point={Point.Consumed} value={1}/></Fragment>,
        points: {consumed: value, exhausted: 0, channeled: 0},
        typeValue: currentPoints.consumed
      };
      break;
    case Point.Channeled:
      preview = {
        currentTypeDisplay: <Fragment>K&nbsp;<PointDisplay point={Point.Channeled} value={1}/></Fragment>,
        points: {consumed: 0, exhausted: 0, channeled: value},
        typeValue: currentPoints.channeled
      };
      break;
    case Point.Exhausted:
      preview = {
        currentTypeDisplay: <Fragment>E&nbsp;<PointDisplay point={Point.Exhausted} value={1}/></Fragment>,
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
    <Row className="justify-content-center">
      <Col className="col-auto">
        <PointsPreview {...{baseCapacity, totalCurrentPoints}}
                       showPenalties={false}
                       points={preview.points}
                       onAppliedPoints={_ => setValue(_ => 0)}/>
      </Col>
    </Row>
    <Row className="PointsEditor-controls align-items-center py-2">
      <Col xs={2} className={"PointsEditor-switch"}>
        <button type="button" role="button" className="btn btn-primary" onClick={toggleType}>
          <p>{preview.currentTypeDisplay}</p>
          <p>(wechseln)</p></button>
      </Col>
      <Col xs={2}>
        <input type="number" className="PointsEditor-edit" min={preview.minAmount} max={preview.maxAmount} value={value}
               onChange={e => setValue(e.target.valueAsNumber)}/>
      </Col>
      <Col>
        <FormRange className="PointsEditor-range" min={preview.minAmount} max={preview.maxAmount} value={value}
                   onChange={e => setValue(e.target.valueAsNumber)}
                   step={1}/>
      </Col>
    </Row>
  </div>;
};