import {Fragment, FunctionComponent, ReactElement, useState} from "react";
import {Point, PointDisplay} from "../PointDisplay";
import {PointsVec} from "../char";
import {PointsPreview} from "../PointsPreview";
import "./PointsEditor.scss";
import {Col, FloatingLabel, FormControl, Row} from "react-bootstrap";
import FormRange from "react-bootstrap/FormRange";
import {parse, render} from "../kevExpr";
import {PointsPreviewData} from "../drag";

interface IPointsEditorProps {
  lineCapacity: number;
  totalCapcity: number;
  currentPoints: PointsVec;
  showPenalties: boolean;
  onReceivePoints: (points: PointsPreviewData) => void;
}

interface PreviewData {
  currentTypeDisplay: ReactElement<any, any>;
  points: PointsVec;
  typeValue: number;
  minAmount: number;
  maxAmount: number;
  totalCurrentPoints: number;
}

function toPointsVector(value: number, type: Point): PointsVec {
  switch (type) {
    case Point.Consumed:
      return {consumed: value, channeled: 0, exhausted: 0};
    case Point.Channeled:
      return {consumed: 0, channeled: value, exhausted: 0};
    case Point.Exhausted:
      return {consumed: 0, channeled: 0, exhausted: value};
    default:
      throw Error("Invalid point: " + type);
  }
}

function calculatePreview(type: Point, value: PointsVec, currentPoints: PointsVec, totalCapcity: number): PreviewData {
  let preview: Omit<PreviewData, "minAmount" | "maxAmount" | "totalCurrentPoints">;
  switch (type) {
    case Point.Consumed:
      preview = {
        currentTypeDisplay: <Fragment>V&nbsp;<PointDisplay point={Point.Consumed} value={1}/></Fragment>,
        points: value,
        typeValue: currentPoints.consumed
      };
      break;
    case Point.Channeled:
      preview = {
        currentTypeDisplay: <Fragment>K&nbsp;<PointDisplay point={Point.Channeled} value={1}/></Fragment>,
        points: value,
        typeValue: currentPoints.channeled
      };
      break;
    case Point.Exhausted:
      preview = {
        currentTypeDisplay: <Fragment>E&nbsp;<PointDisplay point={Point.Exhausted} value={1}/></Fragment>,
        points: value,
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
  const maxAmount = Math.max(0, totalCapcity - totalCurrentPoints);
  const minAmount = Math.min(0, -preview.typeValue);
  return {
    ...preview,
    maxAmount,
    minAmount,
    totalCurrentPoints
  }
}

function inferType(points: PointsVec): Point|null {
  if(points.consumed !== 0 && points.channeled === 0 && points.exhausted === 0) {
    return Point.Consumed;
  } else if(points.consumed === 0 && points.channeled !== 0 && points.exhausted === 0) {
    return Point.Channeled;
  } else if(points.consumed === 0 && points.channeled === 0 && points.exhausted !== 0) {
    return Point.Exhausted;
  } else {
    return null;
  }
}

export const PointsEditor: FunctionComponent<IPointsEditorProps> = ({
  lineCapacity,
  totalCapcity,
  currentPoints,
  onReceivePoints
}) => {
  const [type, setType] = useState(Point.Consumed);
  const [points, setPoints] = useState<PointsVec>(() => ({consumed: 0, exhausted: 0, channeled: 0}));
  const [expr, setExpr] = useState(() => render(points));
  const toggleType = () => {
    let nextType: Point;
    switch (type) {
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
        console.error("Invalid type state: ", type);
        nextType = type;
        break;
    }
    const inferredValue = toPointsVector(points.channeled + points.exhausted + points.consumed, type);
    const preview = calculatePreview(nextType, inferredValue, currentPoints, totalCapcity);
    setPoints(preview.points);
    setExpr(render(preview.points));
    setType(nextType);
  };
  const totalCurrentPoints = currentPoints.channeled + currentPoints.exhausted + currentPoints.consumed;
  const preview = calculatePreview(type, points, currentPoints, totalCapcity);
  const onAppliedPoints = () => {
    const newPoints = {channeled: 0, exhausted: 0, consumed: 0};
    setPoints(_ => newPoints);
    setExpr(render(newPoints));
  };
  return <div className="PointsEditor">
    <Row className="justify-content-center">
      <Col className="col-auto">
        <PointsPreview {...{lineCapacity, totalCapcity, totalCurrentPoints, onAppliedPoints}}
                       showPenalties={false}
                       points={preview.points}/>
      </Col>
    </Row>
    <Row>
      <Col>
        <FloatingLabel label="Punkte (z.B. +K3V1-E2)">
          <FormControl type="text" pattern="(\s+|[kevKEV+-]|\d+)*" placeholder="+K3V1-E2" autoCorrect="off" enterKeyHint="send"
                       value={expr}
                       onKeyUp={e => {
                         console.log('keyup', e.key, e);
                         if(e.key === 'Enter') {
                           onReceivePoints({points});
                           onAppliedPoints();
                         }
                       }}
                       onChange={e => {
                         const newExpr = e.target.value;
                         const newPointsVec = parse(newExpr, type);
                         setPoints(newPointsVec)
                         setExpr(newExpr);
                         const newType = inferType(newPointsVec);
                         if(newType != null) {
                            setType(newType);
                         }
                       }}/>
        </FloatingLabel>
      </Col>
    </Row>
    <Row className="PointsEditor-controls align-items-center py-2">
      <Col xs={2} className={"PointsEditor-switch"}>
        <button type="button" role="button" className="btn btn-primary" onClick={toggleType}>
          <p>{preview.currentTypeDisplay}</p>
          <p>(wechseln)</p></button>
      </Col>
      <Col>
        <FormRange className="PointsEditor-range" min={preview.minAmount} max={preview.maxAmount} value={points.channeled + points.exhausted + points.consumed}
                   onChange={e => {
                     const newPoints = toPointsVector(e.target.valueAsNumber, type);
                     setPoints(newPoints);
                     setExpr(render(newPoints));
                   }}
                   step={1}/>
      </Col>
    </Row>
  </div>;
};